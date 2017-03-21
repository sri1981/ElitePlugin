using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.Entities
{
    class Product : EntityWrapper
    {
        public override string LogicalName { get { return "new_product"; } }

        public Product(IOrganizationService svc, ITracingService tracingSvc, Entity entity)
            : base(svc, tracingSvc, entity) { }

        public Money ProductDefaultExcess
        {
            get
            { return this.Entity.GetAttributeValue<Money>("new_defaultcompulsoryexcess"); }
        }

        public Money ProductDefaultLOI
        {
            get
            { return this.Entity.GetAttributeValue<Money>("new_defaultloi"); }
        }

        public EntityReference LobGroup
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_firstlevellobid"); }
        }

        public EntityReference Lob
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_secondlevellobid"); }
        }

        public EntityReference LobProductLine
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_thirdlevellobid"); }
        }

        public EntityReference LobScheme
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_scheme"); }
        }

        public EntityReference AgreementRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new__agreementid"); }
        }

        public bool CoverDetails
        {
            get { return this.Entity.GetAttributeValue<bool>("new_coverdetails"); }
        }

        public EntityReference TerritoryGroupRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_firstlevelterritoryid"); }
        }

        public EntityReference TerritoryRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_secondlevelterritoryid"); }
        }

        private IEnumerable<Tax> _applicableTaxes;
        public IEnumerable<Tax> ApplicableTaxes
        {
            get
            {
                if (_applicableTaxes != null)
                    return _applicableTaxes;

                _applicableTaxes = RetrieveApplicableTaxes()
                    .Select(t => new Tax(this.OrgService, this.TracingService, t))
                    .ToList();

                return _applicableTaxes;
            }
        }

        private IEnumerable<Commission> _commissions;
        public IEnumerable<Commission> Commissions
        {
            get
            {
                if (_commissions != null)
                    return _commissions;

                _commissions = RetrieveCommissions()
                    .Select(c => new Commission(this.OrgService, this.TracingService, c))
                    .ToList();

                return _commissions;
            }
        }

        private IEnumerable<Risk> _risks;
        public IEnumerable<Risk> Risks
        {
            get
            {
                if (_risks != null)
                    return _risks;

                _risks = RetrieveRisks()
                    .Select(r => new Risk(this.OrgService, this.TracingService, r))
                    .ToList();

                return _risks;
            }
        }

        private IEnumerable<Cover> _covers;
        public IEnumerable<Cover> Covers
        {
            get
            {
                if (_covers != null)
                    return _covers;

                _covers = RetrieveCovers()
                    .Select(c => new Cover(this.OrgService, this.TracingService, c))
                    .ToList();

                return _covers;
            }
        }

        private IEnumerable<Entity> RetrieveRisks()
        {
            var riskQuery = new QueryExpression("new_risk");
            riskQuery.ColumnSet.AllColumns = true;
            riskQuery.Criteria.AddCondition("new_productid", ConditionOperator.Equal, this.Id);

            var result = this.OrgService.RetrieveMultiple(riskQuery);
            return result.Entities;
        }

        private IEnumerable<Entity> RetrieveCovers()
        {
            var coverQuery = new QueryExpression("new_cover");
            coverQuery.ColumnSet.AllColumns = true;
            coverQuery.Criteria.AddCondition("new_productid", ConditionOperator.Equal, this.Id);

            var result = this.OrgService.RetrieveMultiple(coverQuery);
            return result.Entities;
        }

        private IEnumerable<Entity> RetrieveApplicableTaxes()
        {
            var taxQuery = new QueryExpression("new_regionalsettings");
            taxQuery.ColumnSet.AllColumns = true;

            // only taxes for specified territory
            taxQuery.Criteria.AddCondition("new_territory", ConditionOperator.Equal, this.TerritoryRef.Id);

            var result = this.OrgService.RetrieveMultiple(taxQuery);
            return result.Entities;
        }

        private IEnumerable<Entity> RetrieveCommissions()
        {
            var commissionQuery = new QueryExpression("new_commissionsalesdetail");
            commissionQuery.ColumnSet.AllColumns = true;

            var productId = this.Id;

            // agreement or product level
            commissionQuery.Criteria.FilterOperator = LogicalOperator.Or;
            commissionQuery.Criteria.AddCondition("new_productid", ConditionOperator.Equal, productId);

            if(this.AgreementRef != null)
                commissionQuery.Criteria.AddCondition("new__agreementid", ConditionOperator.Equal, this.AgreementRef.Id);

            return this.OrgService.RetrieveMultiple(commissionQuery).Entities;
        }
    }
}
