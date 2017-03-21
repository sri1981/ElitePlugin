using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.Entities
{
    class Tax : EntityWrapper
    {
        public static readonly TaxDateComparer DateComparer = new TaxDateComparer(); 

        public override string LogicalName { get { return "new_regionalsettings"; } }

        public Tax(IOrganizationService svc, ITracingService tracingSvc, Entity entity)
            : base(svc, tracingSvc, entity) { }

        /// <summary>
        /// Peril (Basic Cover) Reference
        /// </summary>
        public EntityReference BasicCoverRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_basiccover"); }
        }

        public EntityReference RegulatoryClassRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_regulatoryclassid"); }
        }

        public DateTime? ValidUntil
        {
            get { return this.Entity.GetAttributeValue<DateTime?>("new_validuntil"); }
        }

        public decimal? TaxPercentage
        {
            get { return this.Entity.GetAttributeValue<decimal?>("new_ipt"); }
        }

        /// <summary>
        /// Overrides default ordering of DateTime? to make null values appear as 
        /// largest.
        /// </summary>
        public class TaxDateComparer : IComparer<DateTime?>
        {
            public int Compare(DateTime? x, DateTime? y)
            {
                if (x == null && y == null)
                    return 0;
                if (x == null)
                    return 1;
                if (y == null)
                    return -1;

                return x.Value.CompareTo(y.Value);
            }
        }
    }
}
