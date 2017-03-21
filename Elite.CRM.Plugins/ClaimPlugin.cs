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
using Elite.CRM.Plugins.Entities;

namespace Elite.CRM.Plugins
{
    public class ClaimPlugin : BasePlugin
    {
        public ClaimPlugin(string unsecureConfig, string secureConfig)
            : base(unsecureConfig, secureConfig)
        {
            // registering event guards against unintended execution in incorrect entity/message/stage
            RegisterEvent(PluginStage.PostOperation, PluginMessage.Create, "new_claim", CreateClaim);
            RegisterEvent(PluginStage.PostOperation, PluginMessage.Update, "new_claim", UpdateClaim);
        }

        public enum ClaimStatus
        {
            Reopened = 100000000,
            Settled = 100000001,
            Declined = 100000002,
            Withdrawn = 100000003,
            NotCovered = 100000004,
            test = 1
        }

        public enum ClaimTransactionStatus
        {
            RecoveryUp = 100000009,
            RecoveryDown = 100000010,
            NewClaim = 100000000,
            ReserveUp = 100000001,
            ReserveDown = 100000002,
        }

        #region CreateClaim
        /// <summary>
        /// Creates the claim.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <exception cref="Exception">
        /// Loss date cannot be after the notification date
        /// or
        /// Please select a Policy
        /// or
        /// Please select a loss type
        /// or
        /// No Basic cover found for the selected loss type
        /// or
        /// No cover found for the policy
        /// or
        /// No Policy version found
        /// or
        /// </exception>
        /// <exception cref="InvalidPluginExecutionException"></exception>
        protected void CreateClaim(LocalPluginContext context)
        {
            try
            {
                Entity claim = context.PluginExecutionContext.InputParameters["Target"] as Entity;
                Entity claimFolder = null;
                Guid claimFolderId = Guid.Empty;
                Entity insuredRisk = null;

                var claimAttributes = new ClaimAttributes(claim, context.OrganizationService, context.TracingService);
                
                //var notificationDate = claim.GetAttributeValue<DateTime>("new_notificationdate");

                //var lossDateField = claim.GetAttributeValue<DateTime>("new_dateofloss");

                if (claimAttributes.LossDate > claimAttributes.NotificationDate)
                    throw new Exception("Loss date cannot be after the notification date");

                if (claimAttributes.NotificationDate > DateTime.Now)
                    throw new Exception("Notification date cannot be later than today");

                if (!claim.Contains("new_policyid"))
                    throw new Exception("Please select a Policy");

                Entity policyFolder = context.OrganizationService.Retrieve("new_policyfolder", claim.GetAttributeValue<EntityReference>("new_policyid").Id, new ColumnSet(true));

                if (claim.Contains("new_insuredrisk"))
                {
                    insuredRisk = context.OrganizationService.Retrieve(claim.GetAttributeValue<EntityReference>("new_insuredrisk"));
                }
                else
                {
                    var retrievedinsuredRisk = context.OrganizationService.RetrieveInsuredRiskForClaim("new_insuredrisk", claimAttributes.Policy.Id); //claim.GetAttributeValue<EntityReference>("new_policyid").Id);

                    if (retrievedinsuredRisk.Count() == 0)
                        throw new Exception("Could not find Insured risk");

                    if (retrievedinsuredRisk.Count() > 1)
                        throw new Exception("Multiple Insured Risks found, please supply the correct one");

                    insuredRisk = retrievedinsuredRisk.FirstOrDefault();
                }

                Guid riskClassId = Guid.Empty;
                if (insuredRisk.Contains("new_riskclassid"))
                    riskClassId = insuredRisk.GetAttributeValue<EntityReference>("new_riskclassid").Id;
                else
                    throw new Exception("Insured risk does not contain Risk Class. Please check Insured Risk");

                Guid productId = policyFolder.GetAttributeValue<EntityReference>("new_product").Id;

                //if (!claim.Contains("new_peril1"))
                //    throw new Exception("Please select a Peril");

                var subPeril = claim.GetAttributeValue<EntityReference>("new_subperil");

                //Entity lossType = context.OrganizationService.Retrieve("new_losstype", claimAttributes.Peril, new ColumnSet(true));

                //var coveredPeril = context.OrganizationService.RetrieveCoveredPeril("new_coveredperil", riskClassId, claimAttributes.SubPeril);

                Guid basicCoverId = Guid.Empty;

                if(claimAttributes.BasicCover == null)
                    throw new Exception("No Basic cover found for the selected loss type");

                basicCoverId = claimAttributes.BasicCover.Id;

                //if (coveredPeril.Count == 0)
                //    throw new Exception("No Covered Peril found for the selected Sub Peril");

                //if (coveredPeril.FirstOrDefault().Contains("new_basiccover"))
                //    basicCoverId = coveredPeril.FirstOrDefault().GetAttributeValue<EntityReference>("new_basiccover").Id;
                //else
                //    throw new Exception("No Basic cover found for the selected loss type");

                var riskSubClassId = insuredRisk.GetAttributeValue<EntityReference>("new_secondlevelriskclass").Id;

                var riskObject = context.OrganizationService.RetrieveRiskObject(riskClassId, riskSubClassId, productId).FirstOrDefault();

                var retrievedCover = context.OrganizationService.RetrieveCover("new_cover", basicCoverId, productId, riskObject.Id);

                if (retrievedCover.Count() == 0)
                    throw new Exception("No cover found for the policy");

                var policyVersion = context.OrganizationService.RetrievePolicyVersion("new_policy", claimAttributes.Policy.Id, claimAttributes.LossDate, retrievedCover.Select(c => c.Id).FirstOrDefault()).FirstOrDefault();

                if (policyVersion == null)
                    throw new Exception("No Policy version found");

                //Guid policyVersionId = policyVersion.FirstOrDefault().Id;

                var premiumPaymentMethod = policyVersion.GetAttributeValue<EntityReference>("new_periodicity");

                if (insuredRisk.Contains("new_gadget"))
                {
                    claim["new_gadget"] = new EntityReference("new_gadget", insuredRisk.GetAttributeValue<EntityReference>("new_gadget").Id);
                }

                #region CommentedCode
                //var insuredRisk = context.OrganizationService.RetrieveRisk("new_insuredrisk", policyVersion.Select(c => c.Id).FirstOrDefault(), productId);

                //if (insuredRisk.Count == 1)
                //{
                //    //if (claim.GetAttributeValue<EntityReference>("new_insuredrisk") == null)
                //    claim["new_insuredrisk"] = claim //new EntityReference("new_insuredrisk", insuredRisk.Select(c => c.Id).FirstOrDefault());
                //}
                //else
                //{
                //    //Think of the logic to go here?
                //}
                #endregion

                var insuredCover = context.OrganizationService.RetrieveInsuredCover("new_insuredcover", policyVersion.Id, retrievedCover.FirstOrDefault().Id).FirstOrDefault();

                //var insuredCoverId = insuredCover.FirstOrDefault().Id;

                if (!claim.Contains("new_policyversion"))
                    claim["new_policyversion"] = policyVersion.ToEntityReference();  //new EntityReference("new_policy", policyVersionId);

                if (!claim.Contains("new_insuredcover") && insuredCover != null)
                    claim["new_insuredcover"] = insuredCover.ToEntityReference(); //new EntityReference("new_insuredcover", insuredCoverId); 

                claim["new_reserve"] = claimAttributes.InitialReserve;

                if (!claim.Contains("new_excessamount"))
                    claim["new_excessamount"] = insuredCover.GetAttributeValue<Money>("new_excessamount");

                if (premiumPaymentMethod != null)
                    claim["new_premiumpaymentmethod"] = premiumPaymentMethod;

                if (claim.Contains("new_initialreserve"))
                    claim["new_incurred"] = claimAttributes.InitialReserve;

                if (!claim.Contains("new_insuredrisk"))
                    claim["new_insuredrisk"] = insuredRisk.ToEntityReference();

                claim["new_associatedincidents"] = 0;

                //Update the claim
                context.OrganizationService.Update(claim);

                var retrievedClaims = context.OrganizationService.RetrieveClaim("new_claim", policyFolder.Id, claimAttributes.LossDate);

                //if (!claim.Contains("new_claimfolder"))
                //    claimFolder = CreateClaimFolder(context.OrganizationService, retrievedClaims, claim, policyFolder);
                //else


                //if (claimFolder != null)
                //    claimFolderId = claimFolder.Id;
                //else if (claim.Contains("new_claimfolder"))
                //    claimFolderId = claimAttributes.ClaimFolder.Id;

                if (claimAttributes.ClaimFolder != null)
                    claimFolder = context.OrganizationService.Retrieve(claimAttributes.ClaimFolder);
                else
                    claimFolder = CreateClaimFolder(context.OrganizationService, retrievedClaims, claim, policyFolder);

                if (claimFolder != null)
                {
                    CalculateAggregates(context.OrganizationService, claimFolder);

                    var claimsCount = context.OrganizationService.RetrieveClaimForClaimFolder(claimFolder.Id);

                    if (claimsCount.Count() > 1)
                    {
                        claim["new_associatedincidents"] = claimsCount.Count();
                        context.OrganizationService.Update(claim);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(ex.Message, new Exception(ex.Message));
            }
        }
        #endregion CreateClaim

        #region CreateClaimFolder
        /// <summary>
        /// Creates the claim folder.
        /// </summary>
        /// <param name="svc">The SVC.</param>
        /// <param name="retrievedClaims">The retrieved claims.</param>
        /// <param name="claim">The claim.</param>
        /// <param name="policyFolder">The policy folder.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private Entity CreateClaimFolder(IOrganizationService svc, IList<Entity> retrievedClaims, Entity claim, Entity policyFolder)
        {
            try
            {
                Guid claimFolderId = Guid.Empty;

                EntityReference policyContact = null;

                EntityReference policyCompany = null;

                if (policyFolder.Contains("new_contact"))
                    policyContact = policyFolder.GetAttributeValue<EntityReference>("new_contact");

                if (policyFolder.Contains("new_account"))
                    policyCompany = policyFolder.GetAttributeValue<EntityReference>("new_account");

                Entity claimFolder = new Entity("new_claimfolder");

                var claimFolderRef = "";

                if (retrievedClaims.Count == 1)
                {
                    //var policyNumber = policyFolder.GetAttributeValue<string>("new_name");
                    claimFolderRef = claim.GetAttributeValue<string>("new_name");
                    if (claimFolderRef != "")
                    {
                        claimFolderRef = claimFolderRef.Replace('L', 'F');
                        //claimFolder = new Entity("new_claimfolder");
                        claimFolder.Attributes["new_name"] = claimFolderRef;
                        if (policyContact != null)
                            claimFolder.Attributes["new_policyholdercontact"] = policyContact;
                        if (policyCompany != null)
                            claimFolder.Attributes["new_policyholdercompany"] = policyCompany;
                        claimFolderId = svc.Create(claimFolder);
                        if (claimFolderId != Guid.Empty)
                        {
                            claim["new_claimfolder"] = new EntityReference("new_claimfolder", claimFolderId);
                            svc.Update(claim);
                        }
                    }
                }
                else
                {
                    claimFolderId = retrievedClaims
                        .Where(c => c.Contains("new_claimfolder"))
                        .Select(c => c.GetAttributeValue<EntityReference>("new_claimfolder").Id)
                        .Distinct()
                        .FirstOrDefault();

                    claim["new_claimfolder"] = new EntityReference("new_claimfolder", claimFolderId);
                    svc.Update(claim);

                }

                return svc.Retrieve("new_claimfolder", claimFolderId, new ColumnSet(true));
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        #endregion
        
        #region UpdateClaim
        /// <summary>
        /// Updates the claim. Fires on update of the claim
        /// </summary>
        /// <param name="context">The context.</param>
        protected void UpdateClaim(LocalPluginContext context)
        {
            try
            {
                Entity claim = context.PluginExecutionContext.InputParameters["Target"] as Entity;

                #region commentedcode
                //Entity preImage = context.PreImage;//Get the Pre Image

                ////Get Reserve from the pre and post Image to compare
                //var preReserve = preImage.GetAttributeValue<Money>("new_reserve");
                //var currReserve = claim.GetAttributeValue<Money>("new_reserve");
                //var preRecoveryOutstanding = preImage.GetAttributeValue<Money>("new_outstanding");
                //var currRecoveryOutstanding = claim.GetAttributeValue<Money>("new_outstanding");
                //int claimTransactionType = 0;

                //var claimStatusCodeValueDeclined = context.OrganizationService.GetOptionsSetValueForStatus("new_claim", "statuscode", "Declined");
                //var claimStatusCodeValueSettled = context.OrganizationService.GetOptionsSetValueForStatus("new_claim", "statuscode", "Settled");
                //var claimStatusCodeReopened = context.OrganizationService.GetOptionsSetValueForStatus("new_claim", "statuscode", "Reopened");

                ////Create the claim transaction
                //Entity createclaimTransaction = new Entity("new_claimtransaction");

                ////We need to create a claim transaction only if the current reserve has value and the current reserve not equal to the previous reserve
                //if (currReserve != null && currReserve.Value != 0)
                //{
                //    //If Pre Reserve is empty we have a new reserve so transaction type is Reserve Up
                //    if (!preImage.Contains("new_reserve"))
                //        claimTransactionType = context.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Reserve Up");
                //    else if ((int)(preReserve.Value) > (int)currReserve.Value)//If Pre Reserve > Current reserve transaction type is Reserve down
                //        claimTransactionType = context.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Reserve Down");
                //    else if ((int)preReserve.Value < (int)currReserve.Value)
                //        claimTransactionType = context.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Reserve Up");
                //    //if (claimTransactionType != 0)
                //    //   createclaimTransaction.Attributes["new_claimtransactiontype"] = new OptionSetValue(claimTransactionType);
                //}
                //if (currRecoveryOutstanding != null && currRecoveryOutstanding.Value != 0)
                //{
                //    if (preRecoveryOutstanding.Value == 0 || preRecoveryOutstanding == null)
                //        claimTransactionType = context.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Recovery Up");
                //    else if (preRecoveryOutstanding != null && (int)(preRecoveryOutstanding.Value) > (int)currRecoveryOutstanding.Value)//If Pre Reserve > Current reserve transaction type is Reserve down
                //        claimTransactionType = context.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Recovery Down");
                //    else if ((int)preRecoveryOutstanding.Value != currRecoveryOutstanding.Value)
                //        claimTransactionType = context.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Recovery Up");
                //}
                //if (claim.Contains("statuscode"))
                //{
                //    if (claim.GetAttributeValue<OptionSetValue>("statuscode").Value == claimStatusCodeValueDeclined)
                //    {
                //        claimTransactionType = claimStatusCodeValueDeclined;
                //    }
                //    else if (claim.GetAttributeValue<OptionSetValue>("statuscode").Value == claimStatusCodeValueSettled)
                //    {
                //        claimTransactionType = claimStatusCodeValueSettled;
                //        if (preImage.GetAttributeValue<OptionSetValue>("statuscode").Value != claim.GetAttributeValue<OptionSetValue>("statuscode").Value)
                //        {
                //            if (claim.Contains("new_originalsettleddate") || preImage.Contains("new_originalsettleddate"))
                //                claim["new_settleddate"] = DateTime.Now;
                //            else
                //            {
                //                claim["new_originalsettleddate"] = DateTime.Now;
                //                claim["new_settleddate"] = DateTime.Now;
                //                context.OrganizationService.Update(claim);
                //            }
                //        }
                //    }
                //    else if (claim.GetAttributeValue<OptionSetValue>("statuscode").Value == claimStatusCodeReopened)
                //    {
                //        claim["new_reopeneddate"] = DateTime.Now;
                //        context.OrganizationService.Update(claim);
                //    }
                //}
                //if (claimTransactionType != 0)
                //    CreateClaimTransaction(claim, context, claimTransactionType);
                #endregion
                #region newcode
                //Entity claim = context.PluginExecutionContext.InputParameters["Target"] as Entity;
                Entity preImage = context.PreImage;//Get the Pre Image

                //Get Reserve from the pre and post Image to compare
                var preReserve = preImage.GetAttributeValue<Money>("new_reserve");
                var currReserve = claim.GetAttributeValue<Money>("new_reserve");
                var preRecoveryOutstanding = preImage.GetAttributeValue<Money>("new_outstanding");
                var currRecoveryOutstanding = claim.GetAttributeValue<Money>("new_outstanding");
                var preClaimStatus = preImage.GetAttributeValue<OptionSetValue>("statuscode").Value;
                int claimTransactionType = 0;
                var claimStatusCodeValueDeclined = (int)ClaimStatus.Declined; //context.OrganizationService.GetOptionsSetValueForStatus("new_claim", "statuscode", "Declined");
                var claimStatusCodeValueSettled = (int)ClaimStatus.Settled; //context.OrganizationService.GetOptionsSetValueForStatus("new_claim", "statuscode", "Settled");
                var claimStatusCodeReopened = (int)ClaimStatus.Reopened;    //context.OrganizationService.GetOptionsSetValueForStatus("new_claim", "statuscode", "Reopened");
                var claimStatusCodeWithdrawn = (int)ClaimStatus.Withdrawn;
                var claimStatusNotCovered = (int)ClaimStatus.NotCovered;

                //Create the claim transaction
                Entity createclaimTransaction = new Entity("new_claimtransaction");

                //We need to create a claim transaction only if the current reserve has value and the current reserve not equal to the previous reserve
                if (currReserve != null && currReserve.Value != 0)
                {
                    //If Pre Reserve is empty we have a new reserve so transaction type is Reserve Up
                    if (preReserve == null)
                    {
                        return;
                        //if(currReserve != null && (int)currReserve.Value > 0)
                        //    claimTransactionType = (int)ClaimTransactionStatus.ReserveUp; //context.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Reserve Up");
                    }
                    else if (preReserve != null && (int)(preReserve.Value) > (int)currReserve.Value)//If Pre Reserve > Current reserve transaction type is Reserve down
                        claimTransactionType = (int)ClaimTransactionStatus.ReserveDown; //context.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Reserve Down");
                    else if ((int)preReserve.Value < (int)currReserve.Value)
                        claimTransactionType = (int)ClaimTransactionStatus.ReserveUp; //context.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Reserve Up");
                    //if (claimTransactionType != 0)
                    //   createclaimTransaction.Attributes["new_claimtransactiontype"] = new OptionSetValue(claimTransactionType);
                }
                if (currRecoveryOutstanding != null && currRecoveryOutstanding.Value != 0)
                {
                    if (preRecoveryOutstanding == null)
                    {
                        if (currRecoveryOutstanding != null && (int)currRecoveryOutstanding.Value > 0)
                            claimTransactionType = (int)ClaimTransactionStatus.RecoveryUp; //context.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Recovery Up");
                    }
                    else if (preRecoveryOutstanding != null && (int)(preRecoveryOutstanding.Value) > (int)currRecoveryOutstanding.Value)//If Pre Reserve > Current reserve transaction type is Reserve down
                        claimTransactionType = (int)ClaimTransactionStatus.RecoveryDown; // ontext.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Recovery Down");
                    else if ((int)preRecoveryOutstanding.Value < currRecoveryOutstanding.Value)
                        claimTransactionType = (int)ClaimTransactionStatus.RecoveryUp; //context.OrganizationService.GetOptionsSetValueForLabel("new_claimtransaction", "new_claimtransactiontype", "Recovery Up");
                }

                if (claimTransactionType != 0)
                    CreateClaimTransaction(context.OrganizationService.Retrieve(claim.LogicalName, claim.Id, new ColumnSet(true)), context, claimTransactionType);

                if (claim.Contains("statuscode"))
                {
                    var status = claim.GetAttributeValue<OptionSetValue>("statuscode").Value;

                    if (preClaimStatus != status)
                    {
                        if (status == claimStatusCodeValueDeclined || status == claimStatusCodeWithdrawn || status == claimStatusNotCovered)
                        {
                            //claimTransactionType = claimStatusCodeValueDeclined;
                            claim["new_originalsettleddate"] = DateTime.Now;
                            claim["new_reserve"] = new Money(0);
                            claim["new_outstanding"] = new Money(0);
                            context.OrganizationService.Update(claim);
                        }
                        else if (claim.GetAttributeValue<OptionSetValue>("statuscode").Value == claimStatusCodeValueSettled)
                        {
                            //claimTransactionType = claimStatusCodeValueSettled;
                            if (preImage.GetAttributeValue<OptionSetValue>("statuscode").Value != claim.GetAttributeValue<OptionSetValue>("statuscode").Value)
                            {
                                if (claim.Contains("new_originalsettleddate") || preImage.Contains("new_originalsettleddate"))
                                    claim["new_settleddate"] = DateTime.Now;
                                else
                                {
                                    claim["new_originalsettleddate"] = DateTime.Now;
                                    claim["new_settleddate"] = DateTime.Now;
                                }
                                context.OrganizationService.Update(claim);
                            }
                        }
                        else if (claim.GetAttributeValue<OptionSetValue>("statuscode").Value == claimStatusCodeReopened)
                        {
                            if (preImage.GetAttributeValue<OptionSetValue>("statuscode").Value != claim.GetAttributeValue<OptionSetValue>("statuscode").Value)
                            {
                                claim["new_reopeneddate"] = DateTime.Now;
                                context.OrganizationService.Update(claim);
                            }
                        }
                    }
                }
                #endregion
                var retrievedClaim = context.OrganizationService.Retrieve("new_claim", claim.Id, new ColumnSet(true));

                if (retrievedClaim.Contains("new_claimfolder"))
                {
                    var claimFolder = context.OrganizationService.Retrieve("new_claimfolder", retrievedClaim.GetAttributeValue<EntityReference>("new_claimfolder").Id, new ColumnSet(true));
                    CalculateAggregates(context.OrganizationService, claimFolder);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        #endregion UpdateClaim

        #region CreateClaimTransaction
        /// <summary>
        /// Creates the claim transaction.
        /// </summary>
        /// <param name="claim">The claim.</param>
        /// <param name="service">The service.</param>
        /// <exception cref="InvalidPluginExecutionException"></exception>
        /// <exception cref="Exception"></exception>
        protected void CreateClaimTransaction(Entity claim, LocalPluginContext context, int transactionType)
        {
            try
            {
                var claimName = claim.GetAttributeValue<string>("new_name");

                //ClaimTransaction(claim, transactionType, context);

                Entity claimTransaction = new Entity("new_claimtransaction");
                claimTransaction["new_paid"] = claim.GetAttributeValue<Money>("new_paid");
                claimTransaction["new_reserve"] = claim.GetAttributeValue<Money>("new_reserve");
                claimTransaction["new_recovered"] = claim.GetAttributeValue<Money>("new_recovered");
                claimTransaction["new_recoveryoutstanding"] = claim.GetAttributeValue<Money>("new_outstanding");
                claimTransaction["new_recoveries"] = claim.GetAttributeValue<Money>("new_recoveries");
                claimTransaction["new_claimtransactiontype"] = new OptionSetValue(transactionType);
                claimTransaction["new_claim"] = new EntityReference("new_claim", claim.Id);
                //claimTransaction.Attributes["new_name"] = claimTransactionName;
                claimTransaction["new_incurred"] = claim.GetAttributeValue<Money>("new_incurred");

                claimTransaction["new_claimtransactiondate"] = DateTime.Now;

                Guid createdClaimTransaction = context.OrganizationService.Create(claimTransaction);
            }
            catch (InvalidPluginExecutionException ex)
            {
                throw new InvalidPluginExecutionException(ex.Message);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        #endregion CreateClaimTransaction

        #region CalculateAggregates
        /// <summary>
        /// Calculates the aggregates for Incurred and Recovered for a Claim Folder
        /// </summary>
        /// <param name="svc">The SVC.</param>
        /// <param name="claimFolder">The claim folder.</param>
        /// <exception cref="Exception"></exception>
        private void CalculateAggregates(IOrganizationService svc, Entity claimFolder)
        {
            try
            {
                decimal claimedAmount = 0, initialReserve = 0, currentReserve = 0, paid = 0, incurred = 0, limitOfIndemnity = 0;

                decimal excessAmount = 0, recovered = 0, recoveryOutstanding = 0, recoveries = 0;

                QueryExpression retrieveClaim = new QueryExpression("new_claim");
                retrieveClaim.ColumnSet.AllColumns = true;
                retrieveClaim.Criteria.AddCondition("new_claimfolder", ConditionOperator.Equal, claimFolder.Id);
                var result = svc.RetrieveMultiple(retrieveClaim).Entities;

                foreach (var res in result)
                {
                    if (res.Contains("new_claimedamount"))
                        claimedAmount += res.GetAttributeValue<Money>("new_claimedamount").Value;
                    if (res.Contains("new_initialreserve"))
                        initialReserve += res.GetAttributeValue<Money>("new_initialreserve").Value;
                    if (res.Contains("new_reserve"))
                        currentReserve += res.GetAttributeValue<Money>("new_reserve").Value;
                    if (res.Contains("new_paid"))
                        paid += res.GetAttributeValue<Money>("new_paid").Value;
                    if (res.Contains("new_incurred"))
                        incurred += res.GetAttributeValue<Money>("new_incurred").Value;
                    if (res.Contains("new_limitofindemnity"))
                        limitOfIndemnity += res.GetAttributeValue<Money>("new_limitofindemnity").Value;
                    if (res.Contains("new_excessamount"))
                        excessAmount += res.GetAttributeValue<Money>("new_excessamount").Value;
                    if (res.Contains("new_recovered"))
                        recovered += res.GetAttributeValue<Money>("new_recovered").Value;
                    if (res.Contains("new_outstanding"))
                        recoveryOutstanding += res.GetAttributeValue<Money>("new_outstanding").Value;
                    if (res.Contains("new_recoveries"))
                        recoveries += res.GetAttributeValue<Money>("new_recoveries").Value;
                }

                claimFolder["new_claimedamount"] = new Money(claimedAmount);
                claimFolder["new_initialreserve"] = new Money(initialReserve);
                claimFolder["new_currentreserve"] = new Money(currentReserve);
                claimFolder["new_paid"] = new Money(paid);
                claimFolder["new_incurred"] = new Money(incurred);
                claimFolder["new_limitofindemnity"] = new Money(limitOfIndemnity);
                claimFolder["new_excessamount"] = new Money(excessAmount);
                claimFolder["new_recovered"] = new Money(recovered);
                claimFolder["new_recoveryoutstanding"] = new Money(recoveryOutstanding);
                claimFolder["new_recoveries"] = new Money(recoveries);

                svc.Update(claimFolder);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        #endregion
    }
}
