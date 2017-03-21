using Elite.CRM.Plugins.ErrorHandling;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.Meta
{
    internal class EntityMetaCache
    {
        private static TimeSpan TimestampExpiration = TimeSpan.FromMinutes(10);
        private static bool CacheEnabled = true;

        private object _lock = new object();

        private string _timestamp;
        private DateTime? _lastTimestampRefresh;
        private Dictionary<string, EntityMetadata> _cache;

        internal EntityMetaCache()
        {
            _cache = new Dictionary<string, EntityMetadata>();
        }

        internal EntityMetadata RetrieveEntity(IOrganizationService svc, string logicalName)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(logicalName, "logicalName");

            if (!CacheEnabled)
                return RetrieveEntityMetadata(svc, logicalName);

            lock (_lock)
            {
                if (_lastTimestampRefresh == null || // metadata never retrieved
                    !_cache.ContainsKey(logicalName) || // no meta for entity
                    DateTime.UtcNow >= _lastTimestampRefresh.Value.Add(TimestampExpiration)) // metadata expired
                {
                    return RefreshMeta(svc, logicalName);
                }
                else
                {
                    return _cache[logicalName];
                }
            }
        }

        private EntityMetadata RefreshMeta(IOrganizationService svc, string logicalName)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(logicalName, "logicalName");

            var ts = RetrieveTimestamp(svc);

            // if timestamp is different, all cached entities are not valid anymore
            if (ts != _timestamp)
            {
                _timestamp = ts;
                _lastTimestampRefresh = DateTime.UtcNow;

                var meta = RetrieveEntityMetadata(svc, logicalName);
                _cache = new Dictionary<string, EntityMetadata>();
                _cache.Add(logicalName, meta);

                return meta;
            }
            else if (!_cache.ContainsKey(logicalName))
            {
                var meta = RetrieveEntityMetadata(svc, logicalName);
                _cache.Add(logicalName, meta);

                return meta;
            }
            
            return _cache[logicalName];
        }


        private EntityMetadata RetrieveEntityMetadata(IOrganizationService svc, string logicalName)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(logicalName, "logicalName");

            var req = new RetrieveEntityRequest()
            {
                EntityFilters = EntityFilters.Attributes,
                LogicalName = logicalName,
                RetrieveAsIfPublished = false
            };

            var resp = svc.Execute(req) as RetrieveEntityResponse;

            return resp.EntityMetadata;
        }

        private string RetrieveTimestamp(IOrganizationService svc)
        {
            ThrowIf.Argument.IsNull(svc, "svc");

            var req = new RetrieveTimestampRequest();
            var resp = svc.Execute(req) as RetrieveTimestampResponse;

            return resp.Timestamp;
        }

    }

}
