using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Crm.Sdk;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using System.ServiceModel;

namespace Elite.CRM.Plugins
{
    public class ClaimRecoveryPlugin : BasePlugin
    {
        public ClaimRecoveryPlugin(string unsecureConfig, string secureConfig)
            : base(unsecureConfig, secureConfig)
        {
            // registering event guards against unintended execution in incorrect entity/message/stage
            RegisterEvent(PluginStage.PostOperation, PluginMessage.Create, "new_claimrecovery", CreateClaimRecovery);
        }

        #region CreateClaimRecovery
        protected void CreateClaimRecovery(LocalPluginContext context)
        {
            try
            {
                Entity claimRecovery = context.PluginExecutionContext.InputParameters["Target"] as Entity;
                Guid claimId = claimRecovery.GetAttributeValue<EntityReference>("new_claim").Id;
                var retrievedClaim = context.OrganizationService.Retrieve("new_claim", claimId, new ColumnSet(true));
                var incurred = retrievedClaim.GetAttributeValue<Money>("new_incurred").Value;
                decimal recoveryOutstanding = 0;
                decimal recoveryAmount = 0;

                if(retrievedClaim.Contains("new_outstanding"))
                    recoveryOutstanding = retrievedClaim.GetAttributeValue<Money>("new_outstanding").Value;

                if (claimRecovery.Contains("new_recoveryamount"))
                    recoveryAmount = claimRecovery.GetAttributeValue<Money>("new_recoveryamount").Value;

                recoveryOutstanding = recoveryOutstanding - recoveryAmount;

                if (recoveryOutstanding < 0)
                    recoveryOutstanding = 0;

                var retrievedClaimRecovery = context.OrganizationService.RetrieveClaimTransactionsorPayments("new_claimrecovery", claimId);
                decimal totalRecovered = 0;

                foreach (var recovered in retrievedClaimRecovery)
                {
                    if (recovered.Contains("new_recoveryamount"))
                    {
                        totalRecovered += (decimal)recovered.GetAttributeValue<Money>("new_recoveryamount").Value;
                    }
                }

                decimal recoveries = 0;
                recoveries = recoveryOutstanding + totalRecovered;

                Entity claim = new Entity("new_claim");
                claim.Id = claimId;
                claim["new_recovered"] = new Money(totalRecovered);

                if (recoveries >= 0)
                    claim["new_recoveries"] = new Money(recoveries);

                if (recoveryOutstanding >= 0)
                    claim["new_outstanding"] = new Money(recoveryOutstanding); // new Money(recoveryOutstanding - (decimal)recoveryAmount.Value);

                if (incurred - recoveries > 0)
                    claim["new_incurred"] = new Money(incurred - recoveries);
                else
                    claim["new_incurred"] = new Money(0);

                context.OrganizationService.Update(claim);

                var createdClaimTransaction = CreateClaimRecoveryTransaction(context.OrganizationService, context.OrganizationService.Retrieve(claim.LogicalName, claim.Id, new ColumnSet(true)), claimRecovery);

                if(createdClaimTransaction != null)
                {
                    claimRecovery["new_claimtransaction"] = createdClaimTransaction.ToEntityReference();
                    context.OrganizationService.Update(claimRecovery);
                }
            }
            catch(Exception ex)
            {
                throw new Exception("Claim Recovery Plugin : " + ex.Message);
            }
        }
        #endregion

        private Entity CreateClaimRecoveryTransaction(IOrganizationService svc, Entity retrievedClaim, Entity claimRecovery)
        {
            int claimTransactionType = 0;
            Guid claimTransactionId = Guid.Empty;
            int recoveryClass;

            var recoveryOutstanding = retrievedClaim.GetAttributeValue<Money>("new_outstanding");
            var recoveryAmount = claimRecovery.GetAttributeValue<Money>("new_recoveryamount");

            if (claimRecovery.Contains("new_recoveryclass"))
            {
                recoveryClass = claimRecovery.GetAttributeValue<OptionSetValue>("new_recoveryclass").Value;
                var recoveryClassLabel = svc.GetOptionSetValueLabel("new_claimrecovery", "new_recoveryclass", recoveryClass);

                if (recoveryClassLabel.ToLower() == "partial recovery")
                    claimTransactionType = svc.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Partial Recovery");
                else
                    claimTransactionType = svc.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Full Recovery");
            }

            Entity claimTransaction = new Entity("new_claimtransaction");

            if (claimRecovery.Contains("new_recoveryamount") && claimRecovery.GetAttributeValue<Money>("new_recoveryamount") != null)
            {
                claimTransaction["new_recovered"] = claimRecovery.GetAttributeValue<Money>("new_recoveryamount");
                if (claimTransactionType != 0)
                    claimTransaction["new_claimtransactiontype"] = new OptionSetValue(claimTransactionType);
                claimTransaction["new_claim"] = new EntityReference("new_claim", retrievedClaim.Id);
                claimTransaction["new_claimtransactiondate"] = DateTime.Now;
                claimTransaction["new_incurred"] = retrievedClaim.GetAttributeValue<Money>("new_incurred");
                claimTransaction["new_recovered"] = retrievedClaim.GetAttributeValue<Money>("new_recovered");
                claimTransaction["new_recoveryoutstanding"] = retrievedClaim.GetAttributeValue<Money>("new_outstanding");
                claimTransaction["new_recoveries"] = retrievedClaim.GetAttributeValue<Money>("new_recoveries");
                claimTransactionId = svc.Create(claimTransaction);
                return svc.Retrieve("new_claimtransaction", claimTransactionId, new ColumnSet(true)); 
            }
            return null;
        }
    }
}
