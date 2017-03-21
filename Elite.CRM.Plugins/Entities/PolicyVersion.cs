using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.Entities
{
    enum PolicyVersionTransactionType
    {
        NewPolicy = 100000000,
        Mta = 100000001,
        Lapse = 100000002,
        Renewal = 100000003,
        Cancellation = 100000004,
        Quote = 100000005,
    }

    enum PolicyVersionStatus
    {
        RequestForQuote = 100000004,
        Quote = 100000005,
        Underwriting = 100000000,
        Draft = 100000008,
        New = 100000001,
        Cancelled = 100000002,
        CoverEnd = 100000003,
        Renewed = 100000006,
        Endorsement = 100000007,
    }

    sealed class PolicyVersion : EntityWrapper
    {
        public override string LogicalName { get { return "new_policy"; } }

        public PolicyVersion(IOrganizationService svc, ITracingService tracingSvc, Entity entity)
            : base(svc, tracingSvc, entity) { }

        public Money LimitOfIndemnity
        {
            get {return this.Entity.GetAttributeValue<Money>("new_limitofindemnity"); }
        }

        public Money Excess
        {
            get { return this.Entity.GetAttributeValue<Money>("new_excess"); }
        }

        public PolicyVersionTransactionType? TransactionType
        {
            get
            {
                var transactionType = this.Entity.GetAttributeValue<OptionSetValue>("new_transactiontype");
                if (transactionType == null)
                    return null;

                return transactionType.ToEnum<PolicyVersionTransactionType>();
            }
        }

        public PolicyInputChannel? InputChannel
        {
            get
            {
                var transactionType = this.Entity.GetAttributeValue<OptionSetValue>("new_inputchannel");
                if (transactionType == null)
                    return null;

                return transactionType.ToEnum<PolicyInputChannel>();
            }
        }

        public Policy PolicyFolder
        {
            get
            {
                var policyRef = this.Entity.GetAttributeValue<EntityReference>("new_policy");
                if (policyRef == null)
                    return null;

                var policy = this.OrgService.Retrieve(policyRef);
                return new Policy(this.OrgService, this.TracingService, policy);
            }
        }

        public string PolicyVersionNumber
        {
            get { return this.Entity.GetAttributeValue<string>("new_name"); }
        }

        public DateTime? TransactionEffectiveDate
        {
            get { return this.Entity.GetAttributeValue<DateTime?>("new_commencementofcover"); }
        }

        public DateTime? TransactionExpiryDate
        {
            get { return this.Entity.GetAttributeValue<DateTime?>("new_endofcover"); }
        }

        //public DateTime CancelledDate
        //{
        //    get { return this.Entity.GetAttributeValue<DateTime>("new_policycancelleddate"); }
        //}

        public EntityReference ProductRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_productid"); }
        }

        private Product _product;
        public Product Product
        {
            get
            {
                if (_product != null)
                    return _product;

                if (this.ProductRef == null)
                    return null;

                var productEntity = OrgService.Retrieve(this.ProductRef);
                _product = new Product(OrgService, TracingService, productEntity);

                return _product;
            }
        }

        public Money BasePremium
        {
            get { return this.Entity.GetAttributeValue<Money>("new_basepremium"); }
        }

        public Money GrossPremium
        {
            get { return this.Entity.GetAttributeValue<Money>("new_grosspremium"); }
        }

        public Money Commission
        {
            get { return this.Entity.GetAttributeValue<Money>("new_commission"); }
        }

        private IEnumerable<InsuredRisk> _insuredRisks;
        public IEnumerable<InsuredRisk> InsuredRisks
        {
            get
            {
                if (_insuredRisks != null)
                    return _insuredRisks;

                _insuredRisks = RetrieveInsuredRisks();
                return _insuredRisks;
            }
        }

        /// <summary>
        /// Retrieves insured risks for this policy version.
        /// </summary>
        /// <returns>Collection of InsureRisk objects.</returns>
        private IEnumerable<InsuredRisk> RetrieveInsuredRisks()
        {
            var insRiskQuery = new QueryExpression("new_insuredrisk");
            insRiskQuery.ColumnSet.AllColumns = true;
            insRiskQuery.Criteria.AddCondition("new_policyid", ConditionOperator.Equal, this.Id);

            var results = this.OrgService.RetrieveMultiple(insRiskQuery);
            return results.Entities
                .Select(e => new InsuredRisk(this.OrgService, this.TracingService, e))
                .ToList();
        }

        private IEnumerable<InsuredCover> _insuredCovers;
        public IEnumerable<InsuredCover> InsuredCovers
        {
            get
            {
                if (_insuredCovers != null)
                    return _insuredCovers;

                _insuredCovers = RetrieveInsuredCovers();
                return _insuredCovers;
            }
        }

        /// <summary>
        /// Retrieves insured covers for this policy version.
        /// </summary>
        /// <returns>Collection of InsureCover objects.</returns>
        private IEnumerable<InsuredCover> RetrieveInsuredCovers()
        {
            var inCoverQuery = new QueryExpression("new_insuredcover");
            inCoverQuery.ColumnSet.AllColumns = true;
            inCoverQuery.Criteria.AddCondition("new_policyid", ConditionOperator.Equal, this.Id);

            var results = this.OrgService.RetrieveMultiple(inCoverQuery);
            return results.Entities.Select(e => new InsuredCover(this.OrgService, this.TracingService, e));
        }

        /// <summary>
        /// Retrieves insured cover taxes for all insured covers of policy version.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Entity> RetrieveInsuredCoverTaxes()
        {
            var ictQuery = new QueryExpression("new_insuredcovertax");
            ictQuery.ColumnSet.AllColumns = true;
            ictQuery.Criteria.AddCondition("new_taxid", ConditionOperator.NotNull);

            var icLink = ictQuery.AddLink("new_insuredcover", "new_insuredcoverid", "new_insuredcoverid");
            icLink.LinkCriteria.AddCondition("new_policyid", ConditionOperator.Equal, this.Id);

            return this.OrgService.RetrieveMultiple(ictQuery).Entities;
        }

        /// <summary>
        /// Retrieves insured cover commissions for policy version. 
        /// </summary>
        /// <param name="commissionId"></param>
        /// <returns></returns>
        public IEnumerable<Entity> RetrieveInsuredCoverCommissions()
        {
            var iccQuery = new QueryExpression("new_insuredcovercommission");
            iccQuery.ColumnSet.AllColumns = true;
            iccQuery.Criteria.AddCondition("new_commissionsalesdetailid", ConditionOperator.NotNull);

            var icLink = iccQuery.AddLink("new_insuredcover", "new_insuredcoverid", "new_insuredcoverid");
            icLink.LinkCriteria.AddCondition("new_policyid", ConditionOperator.Equal, this.Id);

            return this.OrgService.RetrieveMultiple(iccQuery).Entities;
        }

        /// <summary>
        /// Retrieves insured cover reinsurance records for all insured covers of this policy. 
        /// </summary>
        /// <param name="participantId"></param>
        /// <returns></returns>
        public IEnumerable<Entity> RetrieveInsuredCoverReinsurances()
        {
            var icrQuery = new QueryExpression("new_insuredcoverreinsurance");
            icrQuery.ColumnSet.AllColumns = true;
            icrQuery.Criteria.AddCondition("new_reinsurancecontract", ConditionOperator.NotNull);
            icrQuery.Criteria.AddCondition("new_participant", ConditionOperator.NotNull);

            var icLink = icrQuery.AddLink("new_insuredcover", "new_insuredcover", "new_insuredcoverid");
            icLink.LinkCriteria.AddCondition("new_policyid", ConditionOperator.Equal, this.Id);

            return this.OrgService.RetrieveMultiple(icrQuery).Entities;
        }

        /// <summary>
        /// Retrieves policy commission for specified participant (company or contact) and role type.
        /// </summary>
        /// <param name="participantRef"></param>
        /// <param name="roleTypeId"></param>
        /// <returns></returns>
        public Entity RetrievePolicyCommission(EntityReference participantRef, Guid roleTypeId)
        {
            var commQuery = new QueryExpression("new_policycommission");
            commQuery.ColumnSet.AllColumns = true;
            commQuery.TopCount = 1;

            commQuery.Criteria.AddCondition("new_policyid", ConditionOperator.Equal, this.Id);
            commQuery.Criteria.AddCondition("new_roleinpolicyid", ConditionOperator.Equal, roleTypeId);

            if (participantRef.LogicalName == "account")
                commQuery.Criteria.AddCondition("new_accountid", ConditionOperator.Equal, participantRef.Id);
            else if (participantRef.LogicalName == "contact")
                commQuery.Criteria.AddCondition("new_contactid", ConditionOperator.Equal, participantRef.Id);

            return this.OrgService.RetrieveMultiple(commQuery).Entities.FirstOrDefault();
        }

    }
}
