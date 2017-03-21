using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.AutoNamingNumbering
{
    public class RolesAutoNumbering : BasePlugin
    {
        private static readonly Dictionary<string, string> NameFormats = new Dictionary<string, string>();
        private static readonly Regex NamedItemRegex = new Regex(@"\{(?<field>[\w|]+)\}");

        static RolesAutoNumbering()
        {
            NameFormats["new_roleinclaim"] = "{new_claim} - {new_contact|new_company} {new_roletype}";
        }

        public RolesAutoNumbering(string unsecureConfig, string secureConfig)
            : base(unsecureConfig, secureConfig)
        {
            // role in agreement
            //RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "", AutoNumberRole);
            //RegisterEvent(PluginStage.PreOperation, PluginMessage.Update, "", AutoNumberRole);
        }

        private void AutoNumberRole(LocalPluginContext context)
        {
            var entityName = context.PluginExecutionContext.PrimaryEntityName;
            if (!NameFormats.ContainsKey(entityName))
                return;

            // TODO get entity from pre-image + target
            var entity = context.PluginExecutionContext.InputParameters["Target"] as Entity;

            var nameFormat = NameFormats[entityName];
            var name = NamedItemRegex.Replace(nameFormat, (match) =>
            {
                if (!match.Groups["field"].Success)
                    return string.Empty;

                // field names are separated by '|', 
                var fieldNames = match.Groups["field"].Value.Split(new[] { "|" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var field in fieldNames)
                {
                    if (!entity.Contains(field))
                        continue;

                    var value = entity[field];
                    if (value is EntityReference)
                    {
                        var entityValue = context.OrganizationService.Retrieve(value as EntityReference);
                    }
                    else
                    {
                        return value.ToString();
                    }
                }

                return string.Empty;
            });

            entity["new_name"] = name;
        }

    }
}
