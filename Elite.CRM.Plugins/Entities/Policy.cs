using Elite.CRM.Plugins;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.Entities
{
    #region Enums 

    internal enum PolicyInputChannel
    {
        Import = 100000000,
        Portal = 100000001,
        Manual = 100000002,
    }

    internal enum PolicyStatus
    {
        Draft = 100000003,
        Quote = 100000004,
        OnCover = 1,
        CancellationRequest = 100000001,
        Cancelled = 100000002,
        CoverEnd = 2,
        Inactive_Cancelled = 100000000,
    }

    public enum PolicyCancelledBy
    {
        Elite = 100000000,
        Broker = 100000001,
        PolicyHolder = 100000002
    }

    public enum PolicyCancellationType
    {
        FromInception = 100000000,
        HalfWayCancellation = 100000001,
        OnExpiry = 100000002
    }

    public enum PolicyCancellationResponsible
    {
        Elite = 100000000,
        Broker = 100000001,
        Policyholder = 100000002
    }
    
    #endregion

    sealed class Policy : EntityWrapper
    {
        public override string LogicalName { get { return "new_policyfolder"; } }

        public Policy(IOrganizationService svc, ITracingService tracingSvc, Entity entity)
            : base(svc, tracingSvc, entity) { }

        public IEnumerable<PolicyVersion> Versions
        {
            get { return RetrievePolicyVersions(); }
        }

        public EntityReference FirstLevelLob
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_firstlevellob"); }
        }

        public EntityReference SecondLevelLob
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_secondlevellob"); }
        }

        public EntityReference ThirdLevelLob
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_thirdlevellob"); }
        }

        public EntityReference PolicyHolderContact
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_contact"); }
        }

        public EntityReference PolicyHolderCompany
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_account"); }
        }

        public EntityReference Broker
        {
            get { return this.Entity.GetAttributeValue<EntityReference>("new_broker"); }
        }

        public DateTime? InceptionDate
        {
            get { return this.Entity.GetAttributeValue<DateTime?>("new_inceptiondate"); }
        }

        public DateTime? ExpiryDate
        {
            get { return this.Entity.GetAttributeValue<DateTime?>("new_expirydate"); }
        }

        public DateTime? CancellationRequestDate
        {
            get { return this.Entity.GetAttributeValue<DateTime?>("new_cancellationrequestdate"); }
        }

        public DateTime? CancellationApprovalDate
        {
            get { return this.Entity.GetAttributeValue<DateTime?>("new_cancellationapprovaldate"); }
        }

        public Product Product
        {
            get
            {
                var productRef = this.Entity.GetAttributeValue<EntityReference>("new_product");
                if (productRef == null)
                    return null;

                var productEntity = OrgService.Retrieve(productRef);
                return new Product(OrgService, TracingService, productEntity);
            }
        }

        private IEnumerable<PolicyVersion> RetrievePolicyVersions()
        {
            var policyVersionQuery = new QueryExpression("new_policy");
            policyVersionQuery.ColumnSet.AllColumns = true;
            policyVersionQuery.Criteria.AddCondition("new_policy", ConditionOperator.Equal, this.Entity.Id);

            var result = OrgService.RetrieveMultiple(policyVersionQuery);
            return result.Entities.Select(e => new PolicyVersion(OrgService, TracingService, e));
        }
    }
}
