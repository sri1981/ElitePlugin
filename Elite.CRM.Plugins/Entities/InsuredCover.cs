using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.Entities
{
    class InsuredCover : EntityWrapper
    {
        public override string LogicalName { get { return "new_insuredcover"; } }

        public InsuredCover(IOrganizationService svc, ITracingService tracingSvc, Entity entity)
            : base(svc, tracingSvc, entity) { }

        public EntityReference CoverRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_coverid"); }
        }

        public Money GrossPremium
        {
            get { return this.Entity.GetAttributeValue<Money>("new_grosspremium"); }
        }

        public Money GrossExcludingTaxPremium
        {
            get { return this.Entity.GetAttributeValue<Money>("new_grossexcludingtax"); }
        }

        public Money NetOfCommissionsPremium
        {
            get { return this.Entity.GetAttributeValue<Money>("new_netpremium"); }
        }

        public Money NetOfTaxAndCommissionsPremium
        {
            get { return this.Entity.GetAttributeValue<Money>("new_netoftaxpremium"); }
        }

        public Money CededPremium
        {
            get { return this.Entity.GetAttributeValue<Money>("new_cededpremium"); }
        }
        
        public Money GrossRetainedPremium
        {
            get { return this.Entity.GetAttributeValue<Money>("new_grossretainedpremium"); }
        }

        public Money NetRetainedPremium
        {
            get { return this.Entity.GetAttributeValue<Money>("new_netretainedpremium"); }
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

                var policyVer = this.OrgService.Retrieve(this.PolicyVersionRef);
                return new PolicyVersion(this.OrgService, this.TracingService, policyVer);
            }

        }

        public EntityReference InsuredRiskRef
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_insuredriskid"); }
        }

        public InsuredRisk InsuredRisk
        {
            get
            {
                if (this.InsuredRiskRef == null)
                    return null;

                var insRisk = this.OrgService.Retrieve(this.InsuredRiskRef);
                return new InsuredRisk(this.OrgService, this.TracingService, insRisk);
            }

        }

        /// <summary>
        /// Retrieves insured cover tax record for specified tax ID. If record does not exist, returns null.
        /// </summary>
        /// <param name="taxId"></param>
        /// <returns></returns>
        public Entity RetrieveInsuredCoverTax(Guid taxId)
        {
            var ictQuery = new QueryExpression("new_insuredcovertax");
            ictQuery.ColumnSet.AllColumns = true;
            ictQuery.TopCount = 1;

            ictQuery.Criteria.AddCondition("new_insuredcoverid", ConditionOperator.Equal, this.Id);
            ictQuery.Criteria.AddCondition("new_taxid", ConditionOperator.Equal, taxId);

            return this.OrgService.RetrieveMultiple(ictQuery).Entities.FirstOrDefault();
        }

        /// <summary>
        /// Retrieves insured cover commission record for specified tax ID. If record does not exist, returns null.
        /// </summary>
        /// <param name="commissionId"></param>
        /// <returns></returns>
        public Entity RetrieveInsuredCoverCommission(Guid commissionId)
        {
            var iccQuery = new QueryExpression("new_insuredcovercommission");
            iccQuery.ColumnSet.AllColumns = true;
            iccQuery.TopCount = 1;

            iccQuery.Criteria.AddCondition("new_insuredcoverid", ConditionOperator.Equal, this.Id);
            iccQuery.Criteria.AddCondition("new_commissionsalesdetailid", ConditionOperator.Equal, commissionId);

            return this.OrgService.RetrieveMultiple(iccQuery).Entities.FirstOrDefault();
        }

        /// <summary>
        /// Retrieves insured cover reinsurance record for reinsurance section ID and participant ID. If record does not exist, returns null.
        /// </summary>
        /// <param name="participantId"></param>
        /// <returns></returns>
        public Entity RetrieveInsuredCoverReinsurance(Guid reinsuranceSectionId, Guid participantId)
        {
            var icrQuery = new QueryExpression("new_insuredcoverreinsurance");
            icrQuery.ColumnSet.AllColumns = true;
            icrQuery.TopCount = 1;

            icrQuery.Criteria.AddCondition("new_insuredcover", ConditionOperator.Equal, this.Id);
            icrQuery.Criteria.AddCondition("new_reinsurancecontract", ConditionOperator.Equal, reinsuranceSectionId);
            icrQuery.Criteria.AddCondition("new_participant", ConditionOperator.Equal, participantId);

            return this.OrgService.RetrieveMultiple(icrQuery).Entities.FirstOrDefault();
        }

        /// <summary>
        /// Retrieves a reinsurance contract for insured cover based on the policy version inception date and reinsurance contract
        /// connections to individual covers.
        /// </summary>
        /// <param name="context">Local plug-in context.</param>
        /// <param name="insuredCover">Insured cover, for which Reinsurance contract will be retrieved</param>
        /// <param name="method">Reinsurance method (arrangement) to filter reinsurance contracts.</param>
        /// <returns></returns>
        public ReinsuranceContract RetrieveReinsuranceContract(ReinsuranceSectionMethod? method = null)
        {
            var policyVersion = this.PolicyVersion;

            if (policyVersion.TransactionType == PolicyVersionTransactionType.Quote)
                return null;

            var contractQuery = new QueryExpression("new_reinsuranceagreement");
            contractQuery.ColumnSet.AllColumns = true;

            // policy version inception date must be between contract's inception and expiry
            contractQuery.Criteria.AddCondition("new_incepciondate", ConditionOperator.OnOrBefore, policyVersion.TransactionEffectiveDate);
            contractQuery.Criteria.AddCondition("new_expirydate", ConditionOperator.OnOrAfter, policyVersion.TransactionEffectiveDate);

            if (method != null)
            {
                var sectionLink = contractQuery.AddLink("new_reinscontract", "new_reinsuranceagreementid", "new_reinsuranceagreement");
                sectionLink.LinkCriteria.AddCondition("new_reinsurancearrangement", ConditionOperator.Equal, (int)method.Value);
            }

            var reinsuredCoverLink = contractQuery.AddLink("new_reinsuredcover", "new_reinsuranceagreementid", "new_reinsuranceagreement");
            reinsuredCoverLink.LinkCriteria.AddCondition("new_cover", ConditionOperator.Equal, this.CoverRef.Id);

            var contracts = this.OrgService.RetrieveMultiple(contractQuery).Entities;
            return contracts.Select(e => new ReinsuranceContract(this.OrgService, this.TracingService, e)).FirstOrDefault();
        }
    }
}
