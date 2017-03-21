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
    public class ClaimPaymentPlugin : BasePlugin
    {
        public ClaimPaymentPlugin(string unsecureConfig, string secureConfig)
            : base(unsecureConfig, secureConfig)
        {
            // registering event guards against unintended execution in incorrect entity/message/stage
            RegisterEvent(PluginStage.PostOperation, PluginMessage.Create, "new_payment", CreateClaimPayment);
        }

        //This has been moved to the claim transaction plugin
        #region CreateClaimPayment
        protected void CreateClaimPayment(LocalPluginContext context)
        {
            try
            {
                decimal reserveAdjustment = 0;
                Entity claimPayment = context.PluginExecutionContext.InputParameters["Target"] as Entity;

                Guid claimId = claimPayment.GetAttributeValue<EntityReference>("new_claim").Id;

                var retrievedClaim = context.OrganizationService.Retrieve("new_claim", claimId, new ColumnSet(true));

                var status = context.OrganizationService.GetOptionSetValueLabel("new_claim", "statuscode", retrievedClaim.GetAttributeValue<OptionSetValue>("statuscode").Value);

                //if (status.ToLower() != "settled")
               // {
                    var currentReserve = retrievedClaim.GetAttributeValue<Money>("new_reserve").Value;

                    var paymentAmount = claimPayment.GetAttributeValue<Money>("new_amount").Value;

                    decimal recoveries = 0;

                    if (retrievedClaim.Contains("new_recoveries"))
                        recoveries = retrievedClaim.GetAttributeValue<Money>("new_recoveries").Value;

                    var newCurrentReserve = currentReserve - paymentAmount;

                    if (claimPayment.Contains("new_reserveadjustment"))
                        reserveAdjustment = claimPayment.GetAttributeValue<Money>("new_reserveadjustment").Value;

                    var retrievedClaimPayments = context.OrganizationService.RetrieveClaimTransactionsorPayments("new_payment", claimId);

                    decimal totalPaid = 0;

                    foreach (var payments in retrievedClaimPayments)
                    {
                        if (payments.Contains("new_amount"))
                        {
                            totalPaid += (decimal)payments.GetAttributeValue<Money>("new_amount").Value;
                        }
                    }

                    decimal incurred = 0;

                    if (reserveAdjustment != 0)
                        incurred = (reserveAdjustment + totalPaid) - recoveries;
                    else if (newCurrentReserve != 0 && reserveAdjustment == 0)
                        incurred = ((decimal)newCurrentReserve + totalPaid) - recoveries;
                    else
                        incurred = totalPaid - recoveries;

                    Entity claim = new Entity("new_claim");
                    claim.Id = claimId;
                    claim["new_paid"] = new Money(totalPaid);

                    if (incurred > 0)
                        claim["new_incurred"] = new Money(incurred);
                    else
                        claim["new_incurred"] = new Money(totalPaid);

                    if (reserveAdjustment != 0)
                    {
                        if (reserveAdjustment < (decimal)claimPayment.GetAttributeValue<Money>("new_amount").Value)
                            throw new Exception("Not enough reserve to make the payment. Please increase the reserve and try again");
                        claim["new_reserve"] = reserveAdjustment;
                    }
                    else if ((decimal)claimPayment.GetAttributeValue<Money>("new_amount").Value > currentReserve)
                        throw new Exception("Not enough reserve to make the payment. Please increase the reserve and try again");
                    else
                        claim["new_reserve"] = new Money(newCurrentReserve);

                    context.OrganizationService.Update(claim);

                    var createdTransaction = CreateClaimPaymentTransactions(context.OrganizationService, context.OrganizationService.Retrieve(claim.LogicalName, claim.Id, new ColumnSet(true)), claimPayment);

                if(createdTransaction != null)
                {
                    claimPayment["new_claimtransaction"] = createdTransaction.ToEntityReference();
                    context.OrganizationService.Update(claimPayment);
                }

                //}
            }
            catch (Exception ex)
            {
                throw new Exception("Claim Payment Plugin : " + ex.Message);
            }
        }
        #endregion CreateClaimPayment

        private Entity CreateClaimPaymentTransactions(IOrganizationService svc, Entity retrievedClaim, Entity claimPayment)
        {
            int claimTransactionType = 0;
            Guid claimTransactionId = Guid.Empty;

            var currentReserve = retrievedClaim.GetAttributeValue<Money>("new_reserve");

            

            var paymentAmount = claimPayment.GetAttributeValue<Money>("new_payment"); 

            var paymentClass = claimPayment.GetAttributeValue<OptionSetValue>("new_paymentclass").Value;

            var paymentClassLabel = svc.GetOptionSetValueLabel("new_payment", "new_paymentclass", paymentClass);

            if (paymentClassLabel.ToLower() == "partial")
                claimTransactionType = svc.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Partial Payment");
            else
                claimTransactionType = svc.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Final Payment");

            Entity claimTransaction = new Entity("new_claimtransaction");
            if (claimPayment.Contains("new_amount") && claimPayment.GetAttributeValue<Money>("new_amount") != null)
            {
                claimTransaction["new_paid"] = claimPayment.GetAttributeValue<Money>("new_amount");
                if (claimTransactionType != 0)
                    claimTransaction["new_claimtransactiontype"] = new OptionSetValue(claimTransactionType);
                claimTransaction["new_claim"] = new EntityReference("new_claim", retrievedClaim.Id);
                claimTransaction["new_claimtransactiondate"] = DateTime.Now;
                claimTransaction["new_incurred"] = retrievedClaim.GetAttributeValue<Money>("new_incurred");
                claimTransaction["new_reserve"] = retrievedClaim.GetAttributeValue<Money>("new_reserve");
                claimTransactionId = svc.Create(claimTransaction);
                return svc.Retrieve("new_claimtransaction", claimTransactionId, new ColumnSet(true));
                
            }
            return null;
        }
    }
}
