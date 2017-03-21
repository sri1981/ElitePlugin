using Elite.CRM.Plugins.ErrorHandling;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.Entities
{
    public enum RiskClassLevel
    {
        RiskClass = 100000000,
        RiskSubclass = 100000001,
    }

    class RiskClass : EntityWrapper
    {
        public override string LogicalName { get { return "new_riskclass"; } }

        public RiskClass(IOrganizationService svc, ITracingService tracingSvc, Entity entity)
            : base(svc, tracingSvc, entity) { }

        public string RiskEntity
        {
            get { return this.Entity.GetAttributeValue<string>("new_riskentity"); }
        }

        public string InsuredRiskLookup
        {
            get { return this.Entity.GetAttributeValue<string>("new_risklookup"); }
        }

        public RiskClassLevel Level
        {
            get { return this.Entity.GetAttributeValue<OptionSetValue>("new_RiskClassLevel").ToEnum<RiskClassLevel>(); }
        }

        /// <summary>
        /// Gets collection of risk identifiers linked to their respective field mappings.
        /// </summary>
        public IEnumerable<Entity> Identifiers
        {
            get { return RetrieveRiskIdentifiers(); }
        }

        /// <summary>
        /// Retrieves all risk identifiers of a specified risk class.
        /// </summary>
        /// <returns>Collection of Risk Identifier entities.</returns>
        private IEnumerable<Entity> RetrieveRiskIdentifiers()
        {
            var idQuery = new QueryExpression("new_riskidentifier");
            idQuery.ColumnSet.AllColumns = true;

            var fieldMapLink = idQuery.AddLink("new_fieldmapping", "new_field", "new_fieldmappingid");
            fieldMapLink.EntityAlias = "new_fieldmapping";
            fieldMapLink.Columns.AllColumns = true;

            idQuery.Criteria.AddCondition("new_riskclass", ConditionOperator.Equal, this.Id);
            idQuery.AddOrder("new_sequence", OrderType.Ascending);

            var results = this.OrgService.RetrieveMultiple(idQuery);
            return results.Entities;
        }

        /// <summary>
        /// Retrieves a first level risk class record based on the name of a risk entity.
        /// </summary>
        /// <param name="svc">IOrganizationService for data retrieval.</param>
        /// <param name="riskEntityName">Name of risk entity, such as new_pet or new_vehicle.</param>
        /// <returns>Risk class entity, where new_riskentity equals to supplied risk entity name. Null if no such record is found.</returns>
        public static Entity RetrieveForEntityName(IOrganizationService svc, string riskEntityName)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNullOrEmpty(riskEntityName, "riskEntityName");

            var riskClassQuery = new QueryExpression("new_riskclass");
            riskClassQuery.ColumnSet.AllColumns = true;

            riskClassQuery.Criteria.AddCondition("new_riskclasslevel", ConditionOperator.Equal, (int)RiskClassLevel.RiskClass);
            riskClassQuery.Criteria.AddCondition("new_riskentity", ConditionOperator.Equal, riskEntityName);

            riskClassQuery.TopCount = 1;

            var result = svc.RetrieveMultiple(riskClassQuery);
            return result.Entities.FirstOrDefault();
        }
    }
}
