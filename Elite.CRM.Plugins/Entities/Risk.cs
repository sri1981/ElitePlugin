using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.Entities
{
    class Risk : EntityWrapper
    {
        public override string LogicalName { get { return "new_risk"; } }

        public Risk(IOrganizationService svc, ITracingService tracingSvc, Entity entity)
            : base(svc, tracingSvc, entity) { }

        public Money RiskDefaultExcess
        {
            get { return this.Entity.GetAttributeValue<Money>("new_defaultcompulsoryexcess"); }
        }

        public Money RiskDefaultLOI
        {
            get { return this.Entity.GetAttributeValue<Money>("new_defaultloi"); }
        }

        public IEnumerable<Cover> Covers
        {
            get
            {
                return RetrieveCovers().Select(c => new Cover(this.OrgService, this.TracingService, c));
            }
        }

        public EntityReference RiskClassRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_firstlevelriskclassid"); }
        }

        private RiskClass _riskClass;
        public RiskClass RiskClass
        {
            get
            {
                if (_riskClass != null)
                    return _riskClass;

                if (this.RiskClassRef == null)
                    return null;

                var riskClassEntity = this.OrgService.Retrieve(this.RiskClassRef);
                _riskClass = new RiskClass(this.OrgService, this.TracingService, riskClassEntity);
                return _riskClass;
            }
        }

        public EntityReference RiskSubClassRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_secondlevelriskclassid"); }
        }

        private RiskClass _riskSubClass;
        public RiskClass RiskSubClass
        {
            get
            {
                if (_riskSubClass != null)
                    return _riskSubClass;

                if (this.RiskSubClassRef == null)
                    return null;

                var riskSubClassEntity = this.OrgService.Retrieve(this.RiskSubClassRef);
                _riskSubClass = new RiskClass(this.OrgService, this.TracingService, riskSubClassEntity);
                return _riskSubClass;
            }
        }

        public int? MinimumNumberOfRisks
        {
            get { return this.Entity.GetAttributeValue<int?>("new_minimumnumberofrisks"); }
        }

        public int? MaximumNumberOfRisks
        {
            get { return this.Entity.GetAttributeValue<int?>("new_maximumnumberofrisks"); }
        }

        public Product Product
        {
            get
            {
                var productRef = this.Entity.GetAttributeValue<EntityReference>("new_Productid");
                if (productRef == null)
                    return null;

                var product = this.OrgService.Retrieve(productRef);
                return new Product(this.OrgService, this.TracingService, product);
            }
        }

        private IEnumerable<Entity> RetrieveCovers()
        {
            var coverQuery = new QueryExpression("new_cover");
            coverQuery.ColumnSet.AllColumns = true;

            coverQuery.Criteria.AddCondition("new_riskid", ConditionOperator.Equal, this.Id);

            var result = OrgService.RetrieveMultiple(coverQuery);
            return result.Entities;
        }

    }
}
