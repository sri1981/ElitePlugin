using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Elite.CRM.Plugins.Entities
{
    enum ReinsuranceSectionMethod
    {
        QuotaShare = 100000000,
        SurplusShare = 100000001,
        XolRisk = 100000002,
        XolCatastrophe = 100000003,
        XolStopLoss = 100000004,
        Facultative = 100000005,
        FacultativeObligatoryTreaty = 100000006,
    }

    enum ReinsuranceSectionCalculation
    {
        GrossPremium = 100000000,
        NetPremium = 100000001,
    }

    class ReinsuranceSection : EntityWrapper
    {
        public override string LogicalName { get { return "new_reinscontract"; } }

        public ReinsuranceSection(IOrganizationService svc, ITracingService tracingSvc, Entity entity)
            : base(svc, tracingSvc, entity) { }

        public ReinsuranceSectionMethod? Method
        {
            get
            {
                var method = this.Entity.GetAttributeValue<OptionSetValue>("new_reinsurancearrangement");
                if (method == null)
                    return null;

                return method.ToEnum<ReinsuranceSectionMethod>();
            }
        }

        public DateTime InceptionDate
        {
            get { return this.Entity.GetAttributeValue<DateTime>("new_inceptiondate"); }
        }

        public DateTime ExpiryDate
        {
            get { return this.Entity.GetAttributeValue<DateTime>("new_expirydate"); }
        }

        public string SectionCode
        {
            get { return this.Entity.GetAttributeValue<string>("new_reinsurancecontractcode"); }
        }

        public decimal? CededPercentage
        {
            get { return this.Entity.GetAttributeValue<decimal?>("new_cededpercentage"); }
        }

        public decimal? RetentionPercentage
        {
            get { return this.Entity.GetAttributeValue<decimal?>("new_retention"); }
        }

        public Money CededAmount
        {
            get { return this.Entity.GetAttributeValue<Money>("new_cededamount"); }
        }

        public Money RetentionAmount
        {
            get { return this.Entity.GetAttributeValue<Money>("new_retentionamount"); }
        }

        public Money LimitAmount
        {
            get { return this.Entity.GetAttributeValue<Money>("new_limitamount"); }
        }

        public ReinsuranceSectionCalculation ReinsuranceCalculation
        {
            get { return this.Entity.GetAttributeValue<OptionSetValue>("new_reinsurancecalculation").ToEnum<ReinsuranceSectionCalculation>(); }
        }

        public IEnumerable<Entity> Participants
        {
            get { return RetrieveParticipants(); }
        }

        private IEnumerable<Entity> RetrieveParticipants()
        {
            var partQuery = new QueryExpression("new_reinsuranceparticipant");
            partQuery.ColumnSet.AllColumns = true;

            partQuery.Criteria.AddCondition("new_reinsurancecontract", ConditionOperator.Equal, this.Entity.Id);

            return this.OrgService.RetrieveMultiple(partQuery).Entities;
        }

    }
}
