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
using Elite.CRM.Plugins.ErrorHandling;
using System.ServiceModel;

namespace Elite.CRM.Plugins
{
    public class ClaimTransaction : BasePlugin
    {
        public ClaimTransaction(string unsecureConfig, string secureConfig)
            : base(unsecureConfig, secureConfig)
        {
            // registering event guards against unintended execution in incorrect entity/message/stage
            RegisterEvent(PluginStage.PostOperation, PluginMessage.Create, "new_claim", CreateClaimTransaction);
            RegisterEvent(PluginStage.PostOperation, PluginMessage.Create, "new_payment", CreateClaimTransaction);
            RegisterEvent(PluginStage.PostOperation, PluginMessage.Create, "new_claimrecovery", CreateClaimTransaction);
            RegisterEvent(PluginStage.PostOperation, PluginMessage.Update, "new_claim", CreateClaimTransactionOnUpdate);
        }

        public enum ClaimStatus
        {
            Reopened = 100000000,
            Settled = 100000001,
            Declined = 100000002,
            Withdrawn = 100000003,
            NotCovered = 100000004,
            test=1
        }

        public enum ClaimTransactionStatus
        {
            RecoveryUp = 100000009,
            RecoveryDown = 100000010,
            NewClaim = 100000000,
            ReserveUp = 100000001,
            ReserveDown = 100000002,
        }

        #region CreateClaimTransaction
        protected void CreateClaimTransaction(LocalPluginContext context)
        {
            try
            {
                #region new_claim
                Entity target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
                if (target.LogicalName == "new_claim")
                {
                    int transactionType = (int)ClaimTransactionStatus.NewClaim; 
                    CreateTransaction(target, context, transactionType);

                    #region commentedcode
                    //Entity claimTransaction = new Entity("new_claimtransaction");
                    ////claimTransaction.Attributes["new_paid"] = target.GetAttributeValue<Money>("new_paid");
                    //if (context.PluginExecutionContext.MessageName.ToLower() == "create")
                    //    claimTransaction.Attributes["new_reserve"] = target.GetAttributeValue<Money>("new_initialreserve");
                    //else
                    //    claimTransaction.Attributes["new_reserve"] = target.GetAttributeValue<Money>("new_reserve");

                    //claimTransaction.Attributes["new_recovered"] = target.GetAttributeValue<Money>("new_recovered");
                    //claimTransaction.Attributes["new_recoveryoutstanding"] = target.GetAttributeValue<Money>("new_outstanding");
                    //claimTransaction.Attributes["new_recoveries"] = target.GetAttributeValue<Money>("new_recoveries");
                    //claimTransaction.Attributes["new_claimtransactiontype"] = new OptionSetValue(transactionType);
                    //claimTransaction.Attributes["new_claim"] = new EntityReference("new_claim", target.Id);
                    ////claimTransaction.Attributes["new_name"] = claimTransactionName;
                    //claimTransaction.Attributes["new_claimtransactiondate"] = DateTime.Now;

                    //Guid createdClaimTransaction = context.OrganizationService.Create(claimTransaction);
                    #endregion
                }
                #endregion new_claim
                #region new_payments
                else if (target.LogicalName == "new_payment")
                {
                    //int claimTransactionType = 0;

                    //Guid claimTransactionId = Guid.Empty;

                    //Entity claimPayment = context.PluginExecutionContext.InputParameters["Target"] as Entity;

                    //var claimTransactionId = CreatePaymentTransaction(context.OrganizationService, claimPayment);

                    #region commentedcode
                    //Guid claimId = claimPayment.GetAttributeValue<EntityReference>("new_claim").Id;

                    //var retrievedClaim = context.OrganizationService.Retrieve("new_claim", claimId, new ColumnSet(true));

                    //var currentReserve = retrievedClaim.GetAttributeValue<Money>("new_reserve");

                    //var paymentAmount = claimPayment.GetAttributeValue<Money>("new_payment");

                    ////ThrowIf.Argument.IsNull(paymentAmount, "payment is null");

                    //var paymentClass = claimPayment.GetAttributeValue<OptionSetValue>("new_paymentclass").Value;

                    //var paymentClassLabel = context.OrganizationService.GetOptionSetValueLabel("new_payment", "new_paymentclass", paymentClass);

                    //if (paymentClassLabel.ToLower() == "partial")
                    //    claimTransactionType = context.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Partial Payment");
                    //else
                    //    claimTransactionType = context.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Final Payment");

                    //Entity claimTransaction = new Entity("new_claimtransaction");
                    //if (claimPayment.Contains("new_amount") && claimPayment.GetAttributeValue<Money>("new_amount") != null)
                    //{
                    //    claimTransaction.Attributes["new_paid"] = claimPayment.GetAttributeValue<Money>("new_amount");
                    //    if (claimTransactionType != 0)
                    //        claimTransaction.Attributes["new_claimtransactiontype"] = new OptionSetValue(claimTransactionType);
                    //    claimTransaction.Attributes["new_claim"] = new EntityReference("new_claim", claimId);
                    //    claimTransaction.Attributes["new_claimtransactiondate"] = DateTime.Now;
                    //    claimTransactionId = context.OrganizationService.Create(claimTransaction);
                    //}
                    #endregion

                    //if (claimTransactionId != Guid.Empty)
                    //{
                    //    claimPayment["new_claimtransaction"] = new EntityReference("new_claimtransaction", claimTransactionId);
                    //    context.OrganizationService.Update(claimPayment);
                    //}

                    #region commentedcode
                    //var retrievedClaimPayments = context.OrganizationService.RetrieveClaimTransactionsorPayments("new_payment", claimId);

                    //decimal totalPaid = 0;

                    //foreach (var payments in retrievedClaimPayments)
                    //{
                    //    if (payments.Contains("new_amount"))
                    //    {
                    //        totalPaid += (decimal)payments.GetAttributeValue<Money>("new_amount").Value;
                    //    }
                    //}

                    //decimal incurred = 0;

                    //if (currentReserve != null)
                    //    incurred = (decimal)currentReserve.Value + totalPaid;
                    //else
                    //    incurred = totalPaid;

                    //Entity claim = new Entity("new_claim");
                    //claim.Id = claimId;
                    //claim["new_paid"] = new Money(totalPaid);
                    //claim["new_incurred"] = new Money(incurred);
                    //if (totalPaid > (decimal)currentReserve.Value)
                    //    throw new Exception("Not enough reserve to make the payment. Please increase the reserve and try again");
                    //else
                    //    claim["new_reserve"] = new Money((decimal) currentReserve.Value - totalPaid);
                    //context.OrganizationService.Update(claim);
                    #endregion
                }
                #endregion new_payments

                #region new_recovery
                else if (target.LogicalName == "new_claimrecovery")
                {
                    //int claimTransactionType = 0;

                    //Guid claimTransactionId = Guid.Empty;

                    //int recoveryClass;

                    Entity claimRecovery = context.PluginExecutionContext.InputParameters["Target"] as Entity;

                    //var claimTransactionId = CreateRecoveryTransaction(context.OrganizationService, claimRecovery);

                    #region commentedcode
                    //Guid claimId = claimRecovery.GetAttributeValue<EntityReference>("new_claim").Id;

                    //var retrievedClaim = context.OrganizationService.Retrieve("new_claim", claimId, new ColumnSet(true));

                    //var recoveryOutstanding = retrievedClaim.GetAttributeValue<Money>("new_outstanding");

                    //var recoveryAmount = claimRecovery.GetAttributeValue<Money>("new_recoveryamount");

                    //if (claimRecovery.Contains("new_recoveryclass"))
                    //{
                    //    recoveryClass = claimRecovery.GetAttributeValue<OptionSetValue>("new_recoveryclass").Value;

                    //    var recoveryClassLabel = context.OrganizationService.GetOptionSetValueLabel("new_claimrecovery", "new_recoveryclass", recoveryClass);


                    //    if (recoveryClassLabel.ToLower() == "partial recovery")
                    //        claimTransactionType = context.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Partial Recovery");
                    //    else
                    //        claimTransactionType = context.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Full Recovery");
                    //}

                    //Entity claimTransaction = new Entity("new_claimtransaction");

                    //if (claimRecovery.Contains("new_recoveryamount") && claimRecovery.GetAttributeValue<Money>("new_recoveryamount") != null)
                    //{
                    //    claimTransaction.Attributes["new_recovered"] = claimRecovery.GetAttributeValue<Money>("new_recoveryamount");
                    //    if (claimTransactionType != 0)
                    //        claimTransaction.Attributes["new_claimtransactiontype"] = new OptionSetValue(claimTransactionType);
                    //    claimTransaction.Attributes["new_claim"] = new EntityReference("new_claim", claimId);
                    //    claimTransaction.Attributes["new_claimtransactiondate"] = DateTime.Now;
                    //    claimTransactionId = context.OrganizationService.Create(claimTransaction);
                    //}
                    #endregion

                    //if (claimTransactionId != Guid.Empty)
                    //{
                    //    claimRecovery["new_claimtransaction"] = new EntityReference("new_claimtransaction", claimTransactionId);
                    //    context.OrganizationService.Update(claimRecovery);
                    //}
                }
                #endregion
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        #endregion

        #region CreateClaimTransactionOnUpdate
        protected void CreateClaimTransactionOnUpdate(LocalPluginContext context)
        {
            try
            {
                //Entity claim = context.PluginExecutionContext.InputParameters["Target"] as Entity;

                //Entity preImage = context.PreImage;//Get the Pre Image

                ////Get Reserve from the pre and post Image to compare
                //var preReserve = preImage.GetAttributeValue<Money>("new_reserve");
                //var currReserve = claim.GetAttributeValue<Money>("new_reserve");
                //var preRecoveryOutstanding = preImage.GetAttributeValue<Money>("new_outstanding");
                //var currRecoveryOutstanding = claim.GetAttributeValue<Money>("new_outstanding");
                //var preClaimStatus = preImage.GetAttributeValue<OptionSetValue>("statuscode").Value;
                //int claimTransactionType = 0;

                //var claimStatusCodeValueDeclined = (int)ClaimStatus.Declined; //context.OrganizationService.GetOptionsSetValueForStatus("new_claim", "statuscode", "Declined");
                //var claimStatusCodeValueSettled = (int)ClaimStatus.Settled; //context.OrganizationService.GetOptionsSetValueForStatus("new_claim", "statuscode", "Settled");
                //var claimStatusCodeReopened = (int)ClaimStatus.Reopened;    //context.OrganizationService.GetOptionsSetValueForStatus("new_claim", "statuscode", "Reopened");
                //var claimStatusCodeWithdrawn = (int)ClaimStatus.Withdrawn;
                //var claimStatusNotCovered = (int)ClaimStatus.NotCovered;

                ////Create the claim transaction
                //Entity createclaimTransaction = new Entity("new_claimtransaction");

                ////We need to create a claim transaction only if the current reserve has value and the current reserve not equal to the previous reserve
                //if (currReserve != null && currReserve.Value != 0)
                //{
                //    //If Pre Reserve is empty we have a new reserve so transaction type is Reserve Up
                //    if (preReserve == null)
                //    {
                //        return;
                //        //if(currReserve != null && (int)currReserve.Value > 0)
                //        //    claimTransactionType = (int)ClaimTransactionStatus.ReserveUp; //context.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Reserve Up");
                //    }
                //    else if (preReserve != null && (int)(preReserve.Value) > (int)currReserve.Value)//If Pre Reserve > Current reserve transaction type is Reserve down
                //        claimTransactionType = (int)ClaimTransactionStatus.ReserveDown; //context.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Reserve Down");
                //    else if ((int)preReserve.Value < (int)currReserve.Value)
                //        claimTransactionType = (int)ClaimTransactionStatus.ReserveUp; //context.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Reserve Up");
                //    //if (claimTransactionType != 0)
                //    //   createclaimTransaction.Attributes["new_claimtransactiontype"] = new OptionSetValue(claimTransactionType);
                //}
                //if (currRecoveryOutstanding != null && currRecoveryOutstanding.Value != 0)
                //{
                //    if (preRecoveryOutstanding == null)
                //    {
                //        if (currRecoveryOutstanding != null && (int)currRecoveryOutstanding.Value > 0)
                //            claimTransactionType = (int)ClaimTransactionStatus.RecoveryUp; //context.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Recovery Up");
                //    }
                //    else if (preRecoveryOutstanding != null && (int)(preRecoveryOutstanding.Value) > (int)currRecoveryOutstanding.Value)//If Pre Reserve > Current reserve transaction type is Reserve down
                //        claimTransactionType = (int)ClaimTransactionStatus.RecoveryDown; // ontext.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Recovery Down");
                //    else if ((int)preRecoveryOutstanding.Value < currRecoveryOutstanding.Value)
                //        claimTransactionType = (int)ClaimTransactionStatus.RecoveryUp; //context.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Recovery Up");
                //}


                //if (claim.Contains("statuscode"))
                //{
                //    var status = claim.GetAttributeValue<OptionSetValue>("statuscode").Value;

                //    if (preClaimStatus != status)
                //    {

                //        if (status == claimStatusCodeValueDeclined || status == claimStatusCodeWithdrawn || status == claimStatusNotCovered)
                //        {
                //            //claimTransactionType = claimStatusCodeValueDeclined;
                //            claim["new_originalsettleddate"] = DateTime.Now;
                //            claim["new_reserve"] = new Money(0);
                //            claim["new_outstanding"] = new Money(0);
                //            context.OrganizationService.Update(claim);
                //        }
                //        else if (claim.GetAttributeValue<OptionSetValue>("statuscode").Value == claimStatusCodeValueSettled)
                //        {
                //            //claimTransactionType = claimStatusCodeValueSettled;
                //            if (preImage.GetAttributeValue<OptionSetValue>("statuscode").Value != claim.GetAttributeValue<OptionSetValue>("statuscode").Value)
                //            {
                //                if (claim.Contains("new_originalsettleddate") || preImage.Contains("new_originalsettleddate"))
                //                    claim["new_settleddate"] = DateTime.Now;
                //                else
                //                {
                //                    claim["new_originalsettleddate"] = DateTime.Now;
                //                    claim["new_settleddate"] = DateTime.Now;
                //                }
                //                context.OrganizationService.Update(claim);
                //            }
                //        }
                //        else if (claim.GetAttributeValue<OptionSetValue>("statuscode").Value == claimStatusCodeReopened)
                //        {
                //            if (preImage.GetAttributeValue<OptionSetValue>("statuscode").Value != claim.GetAttributeValue<OptionSetValue>("statuscode").Value)
                //            {
                //                claim["new_reopeneddate"] = DateTime.Now;
                //                context.OrganizationService.Update(claim);
                //            }
                //        }


                //    }
                //}
                //if (claimTransactionType != 0)
                //    CreateTransaction(claim, context, claimTransactionType);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        #endregion

        #region CreatePaymentTransaction
        private Guid CreatePaymentTransaction(IOrganizationService svc, Entity claimPayment)
        {
            try
            {
                //int claimTransactionType = 0;

                //Guid claimTransactionId = Guid.Empty;

                //Guid claimId = claimPayment.GetAttributeValue<EntityReference>("new_claim").Id;

                //var retrievedClaim = svc.Retrieve("new_claim", claimId, new ColumnSet(true));

                //var currentReserve = retrievedClaim.GetAttributeValue<Money>("new_reserve");

                //var paymentAmount = claimPayment.GetAttributeValue<Money>("new_payment");

                ////ThrowIf.Argument.IsNull(paymentAmount, "payment is null");

                //var paymentClass = claimPayment.GetAttributeValue<OptionSetValue>("new_paymentclass").Value;

                //var paymentClassLabel = svc.GetOptionSetValueLabel("new_payment", "new_paymentclass", paymentClass);

                //if (paymentClassLabel.ToLower() == "partial")
                //    claimTransactionType = svc.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Partial Payment");
                //else
                //    claimTransactionType = svc.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Final Payment");

                //Entity claimTransaction = new Entity("new_claimtransaction");
                //if (claimPayment.Contains("new_amount") && claimPayment.GetAttributeValue<Money>("new_amount") != null)
                //{
                //    claimTransaction["new_paid"] = claimPayment.GetAttributeValue<Money>("new_amount");
                //    if (claimTransactionType != 0)
                //        claimTransaction["new_claimtransactiontype"] = new OptionSetValue(claimTransactionType);
                //    claimTransaction["new_claim"] = new EntityReference("new_claim", claimId);
                //    claimTransaction["new_claimtransactiondate"] = DateTime.Now;
                //    claimTransaction["new_incurred"] = retrievedClaim.GetAttributeValue<Money>("new_incurred");
                //    claimTransaction["new_reserve"] = retrievedClaim.GetAttributeValue<Money>("new_reserve");
                //    claimTransactionId = svc.Create(claimTransaction);
                //    return claimTransactionId;
                //}

                return Guid.Empty;

            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        #endregion

        #region CreateRecoveryTransaction
        private Guid CreateRecoveryTransaction(IOrganizationService svc, Entity claimRecovery)
        {
            try
            {
                //int claimTransactionType = 0;

                //Guid claimTransactionId = Guid.Empty;

                //int recoveryClass;

                //Guid claimId = claimRecovery.GetAttributeValue<EntityReference>("new_claim").Id;

                //var retrievedClaim = svc.Retrieve("new_claim", claimId, new ColumnSet(true));

                //var recoveryOutstanding = retrievedClaim.GetAttributeValue<Money>("new_outstanding");

                //var recoveryAmount = claimRecovery.GetAttributeValue<Money>("new_recoveryamount");

                //if (claimRecovery.Contains("new_recoveryclass"))
                //{
                //    recoveryClass = claimRecovery.GetAttributeValue<OptionSetValue>("new_recoveryclass").Value;

                //    var recoveryClassLabel = svc.GetOptionSetValueLabel("new_claimrecovery", "new_recoveryclass", recoveryClass);


                //    if (recoveryClassLabel.ToLower() == "partial recovery")
                //        claimTransactionType = svc.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Partial Recovery");
                //    else
                //        claimTransactionType = svc.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Full Recovery");
                //}

                //Entity claimTransaction = new Entity("new_claimtransaction");

                //if (claimRecovery.Contains("new_recoveryamount") && claimRecovery.GetAttributeValue<Money>("new_recoveryamount") != null)
                //{
                //    claimTransaction["new_recovered"] = claimRecovery.GetAttributeValue<Money>("new_recoveryamount");
                //    if (claimTransactionType != 0)
                //        claimTransaction["new_claimtransactiontype"] = new OptionSetValue(claimTransactionType);
                //    claimTransaction["new_claim"] = new EntityReference("new_claim", claimId);
                //    claimTransaction["new_claimtransactiondate"] = DateTime.Now;
                //    claimTransaction["new_incurred"] = retrievedClaim.GetAttributeValue<Money>("new_incurred");
                //    claimTransaction["new_recovered"] = retrievedClaim.GetAttributeValue<Money>("new_recovered");
                //    claimTransaction["new_recoveryoutstanding"] = retrievedClaim.GetAttributeValue<Money>("new_outstanding");
                //    claimTransaction["new_recoveries"] = retrievedClaim.GetAttributeValue<Money>("new_recoveries");
                //    claimTransactionId = svc.Create(claimTransaction);
                //    return claimTransactionId;
                //}
                return Guid.Empty;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        #endregion

        #region CreateTransaction
        private void CreateTransaction(Entity claim, LocalPluginContext context, int transactionType)
        {
            try
            {
                Entity claimTransaction = new Entity("new_claimtransaction");
                var i = 0;
                //claimTransaction.Attributes["new_paid"] = target.GetAttributeValue<Money>("new_paid");
                if (context.PluginExecutionContext.MessageName.ToLower() == "create")
                    claimTransaction.Attributes["new_reserve"] = claim.GetAttributeValue<Money>("new_initialreserve");
                else
                    claimTransaction.Attributes["new_reserve"] = claim.GetAttributeValue<Money>("new_reserve");

                claimTransaction.Attributes["new_recovered"] = claim.GetAttributeValue<Money>("new_recovered");
                claimTransaction.Attributes["new_recoveryoutstanding"] = claim.GetAttributeValue<Money>("new_outstanding");
                claimTransaction.Attributes["new_recoveries"] = claim.GetAttributeValue<Money>("new_recoveries");
                claimTransaction.Attributes["new_claimtransactiontype"] = new OptionSetValue(transactionType);
                claimTransaction.Attributes["new_claim"] = claim.ToEntityReference();
                //claimTransaction.Attributes["new_name"] = claimTransactionName;
                claimTransaction.Attributes["new_claimtransactiondate"] = DateTime.Now;

                Guid createdClaimTransaction = context.OrganizationService.Create(claimTransaction);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        #endregion
    }
}
