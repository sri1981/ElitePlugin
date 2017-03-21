using Elite.CRM.Plugins.Entities;
using Elite.CRM.Plugins.ErrorHandling;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.AutoNamingNumbering
{
    public class AutoCodes : BasePlugin
    {
        public AutoCodes(string unsecureConfig, string secureConfig)
            : base(unsecureConfig, secureConfig)
        {
            // null -> any entity
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, null, SetAutoCode);
        }

        /// <summary>
        /// Gets a next auto-code from Numbering entity and puts it into the field specified in numbering.
        /// </summary>
        /// <param name="context">Local plug-in context.</param>
        protected void SetAutoCode(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            if (target == null)
                throw new InvalidPluginExecutionException("Invalid plug-in registration, expecting 'Target' input parameter.");

            var numbering = RetrieveNumberingForEntity(context, target.LogicalName);
            if (numbering == null)
                return;

            if (string.IsNullOrEmpty(numbering.FieldName))
                throw new InvalidPluginExecutionException("Numbering configuration error: Field name is empty.");

            if (numbering.CurrentNumber == null)
                throw new InvalidPluginExecutionException("Numbering configuration error: Current number is empty.");

            if (target.LogicalName == "new_lob1")
            {
                var level = target.GetAttributeValue<OptionSetValue>("new_loblevel");
                numbering = RetrieveNumberingForLOB(context, target.LogicalName, level.Value);
                var code = numbering.GetNextNumber();
                target[numbering.FieldName] = code;
            }
            else //if (ShouldAutoNumber(context.OrganizationService, target)) //this code not needed as the above if statement has been added
            {
                var code = numbering.GetNextNumber();
                target[numbering.FieldName] = code;
            }
        }

        /// <summary>
        /// Retrieves a new_numbering for an entity specified by entity logical name.
        /// </summary>
        /// <param name="context">Local plug-in context.</param>
        /// <param name="entityName">Logical name of entity.</param>
        /// <returns>Numbering record for specified entity or null if not numbering record for entity does not exist.</returns>
        private static Numbering RetrieveNumberingForEntity(LocalPluginContext context, string entityName)
        {
            ThrowIf.Argument.IsNull(context, "context");

            var numberingQuery = new QueryExpression("new_numbering");
            numberingQuery.ColumnSet.AllColumns = true;
            numberingQuery.NoLock = true;

            numberingQuery.Criteria.AddCondition("new_entityname", ConditionOperator.Equal, entityName);
            numberingQuery.TopCount = 1;

            var results = context.OrganizationService.RetrieveMultiple(numberingQuery);
            return results.Entities.Select(n => new Numbering(context.OrganizationService, context.TracingService, n)).FirstOrDefault();
        }

        private static Numbering RetrieveNumberingForLOB(LocalPluginContext context, string entityName, int lobLevel)
        {

            ThrowIf.Argument.IsNull(context, "context");

            var numberingQuery = new QueryExpression("new_numbering");
            numberingQuery.ColumnSet.AllColumns = true;
            numberingQuery.NoLock = true;

            numberingQuery.Criteria.AddCondition("new_entityname", ConditionOperator.Equal, entityName);
            numberingQuery.Criteria.AddCondition("new_loblevel", ConditionOperator.Equal, lobLevel);
            numberingQuery.TopCount = 1;

            var results = context.OrganizationService.RetrieveMultiple(numberingQuery);
            return results.Entities.Select(n => new Numbering(context.OrganizationService, context.TracingService, n)).FirstOrDefault();

        }

        /// <summary>
        /// Checks, if current entity should be auto-numbered (if special conditions apply)
        /// </summary>
        /// <param name="svc"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        private bool ShouldAutoNumber(IOrganizationService svc, Entity e)
        {
            if (e.LogicalName == "new_lob1")
            {
                var level = e.GetAttributeValue<OptionSetValue>("new_loblevel");
                if (level == null)
                    return false;

                return level.ToEnum<LobLevel>() == LobLevel.Scheme;
            }

            // by default, no special conditions apply
            return true;
        }
    }
}
