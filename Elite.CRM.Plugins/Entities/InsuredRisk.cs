using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.Entities
{
    sealed class InsuredRisk : EntityWrapper
    {
        public override string LogicalName { get { return "new_insuredrisk"; } }

        public InsuredRisk(IOrganizationService svc, ITracingService tracingSvc, Entity entity)
            : base(svc, tracingSvc, entity) { }

        public Money RiskExcess
        {
            get { return this.Entity.GetAttributeValue<Money>("new_excess"); }
        }

        public Money RiskLOI
        {
            get { return this.Entity.GetAttributeValue<Money>("new_limitofindemnity"); }
        }

        public RiskClass RiskClass
        {
            get
            {
                var riskClassRef = this.Entity.GetAttributeValue<EntityReference>("new_riskclassid");
                if (riskClassRef == null)
                    return null;

                var riskClassEntity = this.OrgService.Retrieve(riskClassRef);
                return new RiskClass(this.OrgService, this.TracingService, riskClassEntity);
            }
        }

        public RiskClass RiskSubClass
        {
            get
            {
                var riskClassRef = this.Entity.GetAttributeValue<EntityReference>("new_secondlevelriskclass");
                if (riskClassRef == null)
                    return null;

                var riskClassEntity = this.OrgService.Retrieve(riskClassRef);
                return new RiskClass(this.OrgService, this.TracingService, riskClassEntity);
            }
        }

        public EntityReference RiskEntityRef
        {
            get
            {
                var riskClass = this.RiskClass;
                if (riskClass == null || string.IsNullOrEmpty(riskClass.InsuredRiskLookup))
                    return null;

                return this.Entity.GetAttributeValue<EntityReference>(riskClass.InsuredRiskLookup);
            }
        }

        public EntityReference PolicyVersionRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_policyid"); }
        }

        public PolicyVersion PolicyVersion
        {
            get
            {
                if (this.PolicyVersionRef == null)
                    return null;

                var policyVersionEntity = this.OrgService.Retrieve(this.PolicyVersionRef);
                return new PolicyVersion(this.OrgService, this.TracingService, policyVersionEntity);
            }
        }

        public Risk Risk
        {
            get
            {
                var riskRef = this.Entity.GetAttributeValue<EntityReference>("new_riskid");
                if (riskRef == null)
                    return null;

                var risk = this.OrgService.Retrieve(riskRef);
                return new Risk(this.OrgService, this.TracingService, risk);
            }
        }

        public IEnumerable<InsuredCover> InsuredCovers
        {
            get
            {
                return RetrieveInsuredCovers();
            }
        }

        /// <summary>
        /// Retrieves insured covers for this insured risk.
        /// </summary>
        /// <returns>Collection of InsuredCover objects.</returns>
        private IEnumerable<InsuredCover> RetrieveInsuredCovers()
        {
            var inCoverQuery = new QueryExpression("new_insuredcover");
            inCoverQuery.ColumnSet.AllColumns = true;
            inCoverQuery.Criteria.AddCondition("new_insuredriskid", ConditionOperator.Equal, this.Id);

            var results = this.OrgService.RetrieveMultiple(inCoverQuery);
            return results.Entities.Select(e => new InsuredCover(this.OrgService, this.TracingService, e));
        }
    }
}
