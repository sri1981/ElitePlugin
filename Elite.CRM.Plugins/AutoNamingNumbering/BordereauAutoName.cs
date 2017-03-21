using Elite.CRM.Plugins.Entities;
using Elite.CRM.Plugins.ErrorHandling;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.AutoNamingNumbering
{
    public class BordereauAutoName : BasePlugin
    {
        #region Comparer for similarity matching

        private class SimilarityComparer : IComparer<string>
        {
            private string _baseWord;

            public SimilarityComparer(string baseWord)
            {
                _baseWord = baseWord;
            }

            public int Compare(string x, string y)
            {
                var d1 = Distance(x);
                var d2 = Distance(y);
                return d2 - d1;
            }

            public int Distance(string x)
            {
                var s1 = x.Trim().ToLowerInvariant();
                var s2 = _baseWord.Trim().ToLowerInvariant();
                // Levenshtein distance algorithm copy-pasted from http://www.dotnetperls.com/levenshtein
                int m = s1.Length;
                int n = s2.Length;
                int[,] d = new int[n + 1, m + 1];

                if (n == 0)
                    return m;
                if (m == 0)
                    return n;

                for (int i = 0; i <= n; d[i, 0] = i++) { }
                for (int j = 0; j <= m; d[0, j] = j++) { }

                for (int i = 1; i <= n; i++)
                {
                    for (int j = 1; j <= m; j++)
                    {
                        int cost = (s1[j - 1] == s2[i - 1]) ? 0 : 1;
                        d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                    }
                }
                return d[n, m];
            }
        }
        #endregion

        public BordereauAutoName(string unsecureConfig, string secureConfig)
            : base(unsecureConfig, secureConfig)
        {
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Update, "new_fieldmapping", FieldMappingOnUpdate);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_fieldmapping", FieldMappingOnCreate);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_bordereauxtemplatecolumn", TemplateColumnOnCreate);
        }

        protected void TemplateColumnOnCreate(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;

            var bxTemplateRef = target.GetAttributeValue<EntityReference>("new_bordereauxtemplate");
            if (bxTemplateRef == null)
                throw new InvalidPluginExecutionException("Bordereau template is empty.");

            var valueType = target.GetAttributeValue<OptionSetValue>("new_valuetype");

            if (valueType == null || valueType.ToEnum<ColumnValueType>() == ColumnValueType.ColumnMapping)
            {
                // set correct column number based on column label, if number is null
                var columnLabel = target.GetAttributeValue<string>("new_columnlabel");
                var columnNumber = target.GetAttributeValue<int?>("new_columnnumber");
                if (columnNumber == null && !string.IsNullOrEmpty(columnLabel))
                    target["new_columnnumber"] = Utils.LettersToNumber(columnLabel);
            }
            else
            {
                target["new_columnnumber"] = null;
                target["new_columnlabel"] = null;
            }

            var bxTemplate = context.OrganizationService.Retrieve(bxTemplateRef);
            var template = new BordereauTemplate(context.OrganizationService, context.TracingService, bxTemplate);

            var numberOfColumns = template.TemplateColumns.Count();

            var name = "{0} - {1:000}".FormatWith(bxTemplate.GetAttributeValue<string>("new_name"), numberOfColumns + 1);

            target["new_name"] = name;
        }

        protected void FieldMappingOnUpdate(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;

            string entityName = null;
            if (target.Contains("new_destinationentityschemaname"))
            {
                entityName = target.GetAttributeValue<string>("new_destinationentityschemaname").ToLowerInvariant();
                target["new_destinationentityschemaname"] = entityName; // to ensure it's lowercase in result
            }
            else if (context.PreImage.Contains("new_destinationentityschemaname"))
            {
                entityName = context.PreImage.GetAttributeValue<string>("new_destinationentityschemaname");
            }
            else
            {
                throw new InvalidPluginExecutionException("'new_destinationentityschemaname' must not be empty.");
            }

            string fieldName = null;
            if (target.Contains("new_destinationfield"))
            {
                fieldName = target.GetAttributeValue<string>("new_destinationfield").ToLowerInvariant();
                target["new_destinationfield"] = fieldName;
            }
            else if (context.PreImage.Contains("new_destinationfield"))
            {
                fieldName = context.PreImage.GetAttributeValue<string>("new_destinationfield");
            }
            else
            {
                throw new InvalidPluginExecutionException("'new_destinationfield' must not be empty.");
            }

            target["new_name"] = CreateEntityName(context.OrganizationService, entityName, fieldName);
        }

        protected void FieldMappingOnCreate(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            if (!target.Contains("new_destinationentityschemaname"))
                throw new InvalidPluginExecutionException("'new_destinationentityschemaname' must not be empty.");

            if (!target.Contains("new_destinationfield"))
                throw new InvalidPluginExecutionException("'new_destinationfield' must not be empty.");

            var entityName = target.GetAttributeValue<string>("new_destinationentityschemaname").ToLowerInvariant();
            target["new_destinationentityschemaname"] = entityName;

            var fieldName = target.GetAttributeValue<string>("new_destinationfield").ToLowerInvariant();
            target["new_destinationfield"] = fieldName;

            target["new_name"] = CreateEntityName(context.OrganizationService, entityName, fieldName);
        }

        private static string CreateEntityName(IOrganizationService svc, string entityName, string fieldName)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(entityName, "entityName");
            ThrowIf.Argument.IsNullOrEmpty(fieldName, "fieldName");

            var entityReq = new RetrieveEntityRequest()
            {
                EntityFilters = EntityFilters.Entity | EntityFilters.Attributes,
                LogicalName = entityName
            };

            RetrieveEntityResponse entityResp = null;

            try
            {
                entityResp = svc.Execute(entityReq) as RetrieveEntityResponse;
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                throw new InvalidPluginExecutionException("Entity '{0}' not found - it either doesn't exist or you do not have permissions to view it.".FormatWith(entityName), ex);
            }

            var entityDisplayName = entityResp.EntityMetadata.DisplayName.UserLocalizedLabel.Label;

            var fieldMeta = entityResp.EntityMetadata.Attributes.FirstOrDefault(a => a.LogicalName == fieldName);
            if (fieldMeta == null)
            {
                var comparer = new SimilarityComparer(fieldName);
                var topSimilar = entityResp.EntityMetadata.Attributes
                    .Where(a => a.AttributeOf == null && comparer.Distance(a.LogicalName) <= 3)
                    .OrderByDescending(a => a.LogicalName, comparer)
                    .Take(5);

                var suggestions = string.Join(", ", topSimilar.Select(a => a.LogicalName));
                throw new InvalidPluginExecutionException("Entity '{0}' does not contain field '{1}'. Did you mean one of these? {2}".FormatWith(entityName, fieldName, suggestions));
            }

            var fieldDisplayName = fieldMeta.DisplayName.UserLocalizedLabel.Label;

            return "{0} - {1}".FormatWith(entityDisplayName, fieldDisplayName);
        }
    }
}

