using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.Entities
{
    sealed class ClaimAttributes
    {
        Entity _claim = null;
        static IOrganizationService _svc;
        static ITracingService _trace;

        public ClaimAttributes(Entity claim, IOrganizationService svc, ITracingService trace)
        {
            _claim = claim;
            _svc = svc;
            _trace = trace;
        }

        public EntityReference Policy
        {
            get { return _claim.GetAttributeValue<EntityReference>("new_policyid"); }
        }

        public Policy PolicyDetails
        {
            get
            {
                return new Policy(_svc, _trace, _svc.Retrieve(this._claim.GetAttributeValue<EntityReference>("new_policyid")));
            }
        }

        public EntityReference ClaimFolder
        {
            get { return this._claim.GetAttributeValue<EntityReference>("new_claimfolder"); }
        }

        public EntityReference PolicyVersion
        {
            get { return this._claim.GetAttributeValue<EntityReference>("new_policyversion"); }
        }

        public Money ClaimedAmount
        {
            get { return this._claim.GetAttributeValue<Money>("new_claimedamount"); }
        }

        public OptionSetValue ClaimTransactionType
        {
            get { return this._claim.GetAttributeValue<OptionSetValue>("new_claimtransactiontype"); }
        }

        public EntityReference BordereauProcess
        {
            get { return this._claim.GetAttributeValue<EntityReference>("new_bordereauprocess"); }
        }

        public EntityReference Currency
        {
            get { return this._claim.GetAttributeValue<EntityReference>("transactioncurrencyid"); }
        }

        public DateTime NotificationDate
        {
            get { return _claim.GetAttributeValue<DateTime>("new_notificationdate"); }
        }

        public DateTime LossDate
        {
            get { return _claim.GetAttributeValue<DateTime>("new_dateofloss"); }
        }

        public Guid Peril
        {
            get { return _claim.GetAttributeValue<EntityReference>("new_peril1").Id; }
        }

        public EntityReference SubPeril
        {
            get { return _claim.GetAttributeValue<EntityReference>("new_subperil"); }
        }

        public Money InitialReserve
        {
            get { return _claim.GetAttributeValue<Money>("new_initialreserve"); }
        }

        public EntityReference BasicCover
        {
            get { return RetriveBasicCover(); }
        }

        public EntityReference RetriveBasicCover()
        {
            var cause = _svc.Retrieve(this.SubPeril.LogicalName,this.SubPeril.Id, new ColumnSet(true));

            if (cause != null)
                return cause.GetAttributeValue<EntityReference>("new_peril");
                //return cause.GetAttributeValue<EntityReference>("new_basiccoverid");
            else
                return null;
        }

    }
}
