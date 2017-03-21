using Elite.CRM.Plugins.ErrorHandling;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elite.CRM.Plugins.BordereauImport;
using System.ServiceModel;

namespace Elite.CRM.Plugins.Entities
{
    enum ClaimTransactionType
    {
        New = 100000000,
        Settled = 100000004,
        Declined = 100000005
        //Cancelled = 100000002
    }

    sealed class Claim 
    {
        #region members
        private IOrganizationService svc;
        //private IEnumerable<MappedAttribute> claimAttributeValue;
        //private IEnumerable<MappedAttribute> claimValueBasedOnClaimOrder;
        private ClaimAttribute claimAttribute;
        #endregion

        public Claim(IOrganizationService _svc) 
        {
            svc = _svc;
            //claimAttribute = _claimAttribute;
        }
        
        #region CreateOrUpdateClaim
        /// <summary>
        /// Creates the or update claim.
        /// </summary>
        /// <param name="svc">The SVC.</param>
        /// <param name="claimAttributeValue">The claim attribute value.</param>
        /// <param name="claimValueBasedOnCover">The claim value based on cover.</param>
        /// <returns></returns>
        /// <exception cref="Exception">Claim already in CRM</exception>
        public Guid CreateOrUpdateClaim(ClaimAttribute _claimAttribute)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            claimAttribute = _claimAttribute;
            try
            {
                var createdClaimId = Guid.Empty;
                var dateOfLoss = claimAttribute.lossDateField; 
                //Retrieve the claim
                var retrievedClaim = svc.RetrieveClaim("new_claim", claimAttribute.claimFolderReferenceValue, claimAttribute.brokerId, claimAttribute.subPeril.Id, (DateTime?)dateOfLoss);

                if (retrievedClaim.Count > 0)
                    createdClaimId = retrievedClaim.FirstOrDefault().Id;

                //TO DO Update claim if found
                if (retrievedClaim.Count == 0)
                    createdClaimId = CreateClaim();
                                
                return createdClaimId;
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Claim Class ", new Exception(ex.InnerException.Message));
            }
        }
        #endregion

        #region CreateClaimFolder
        /// <summary>
        /// Creates the claim folder.
        /// </summary>
        /// <param name="svc">The SVC.</param>
        /// <param name="createdClaim">The created claim.</param>
        /// <param name="dateOFLoss">The date of loss.</param>
        private void CreateClaimFolder(IOrganizationService svc, Entity createdClaim, DateTime dateOFLoss)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNull(createdClaim, "createdClaim");
            try
            {
                var retrievedClaimForClaimFolder = svc.RetrieveClaim("new_claim", claimAttribute.policy.FirstOrDefault().Id, (DateTime)dateOFLoss);
                
                Guid retrieveClaimFolderId = Guid.Empty;
                Entity claimFolder = new Entity("new_claimfolder");
                var claimFolderRef = "";

                if (retrievedClaimForClaimFolder.Count == 1)
                {
                    claimFolderRef = createdClaim.GetAttributeValue<string>("new_name");

                    if (claimFolderRef != null)
                    {
                        claimFolderRef = claimFolderRef.Replace('L', 'F');
                        claimFolder = new Entity("new_claimfolder");
                        claimFolder.Attributes["new_name"] = claimFolderRef;
                        retrieveClaimFolderId = svc.Create(claimFolder);

                        if (retrieveClaimFolderId != Guid.Empty)
                        {
                            createdClaim["new_claimfolder"] = new EntityReference("new_claimfolder", retrieveClaimFolderId);
                            //svc.Update(createdClaim);
                        }
                    }
                }
                else
                {
                    var claimFolderId = retrievedClaimForClaimFolder.Where(c => c.Contains("new_claimfolder")).Select(c => c.GetAttributeValue<EntityReference>("new_claimfolder").Id).Distinct();
                    if (claimFolderId.FirstOrDefault() != Guid.Empty)
                        createdClaim["new_claimfolder"] = new EntityReference("new_claimfolder", claimFolderId.FirstOrDefault());
                    //svc.Update(createdClaim);
                }

                //var claimsCount = svc.RetrieveClaimForClaimFolder(claimFolder.Id);

                //createdClaim["new_associatedincidents"] = claimsCount.Count();
                //svc.Update(createdClaim);
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Claim Class ", new Exception(ex.Message));
            }
        }
        #endregion CreateClaimFolder

        #region CreateClaim
        /// <summary>
        /// Creates the claim.
        /// </summary>
        /// <param name="claim">The claim.</param>
        /// <param name="excess">The excess.</param>
        /// <returns></returns>
        /// <exception cref="InvalidPluginExecutionException">Claim Class - CreateClaim Method</exception>
        /// <exception cref="Exception"></exception>
        private Guid CreateClaim() //Money excess
        {
            try
            {
                Entity claim = new Entity("new_claim");
                //claim["new_losstypeid"] = claimAttribute.lossType.FirstOrDefault().ToEntityReference();
                claim["new_bordereauprocess"] = new EntityReference("new_import", claimAttribute.bxprocessId);
                claim["new_policyid"] = claimAttribute.policy.FirstOrDefault().ToEntityReference();
                claim["new_incurred"] = new Money(claimAttribute.Reserve.Value);
                //claim["new_peril1"] = claimAttribute.lossType.FirstOrDefault().ToEntityReference();
                claim["new_subperil"] = claimAttribute.subPeril.ToEntityReference();

                if (claimAttribute.insuredRisk != null)
                    claim["new_insuredrisk"] = claimAttribute.insuredRisk.FirstOrDefault().ToEntityReference();
                claim["new_initialreserve"] = new Money(claimAttribute.Reserve.Value);
                
                if (claimAttribute.premiumPaymentMethod != null)
                    claim["new_premiumpaymentmethod"] = claimAttribute.premiumPaymentMethod;
                claim["new_dateofloss"] = claimAttribute.lossDateField;
                claim["new_notificationdate"] = claimAttribute.claimNotificationDate.AsDateTime();
                if (claimAttribute.ClaimedAmount != null)
                    claim["new_claimedamount"] = new Money(claimAttribute.ClaimedAmount.Value);
                claim["new_losstypedescription"] = claimAttribute.lossTypeDescription;
                claim["new_claimreference"] = claimAttribute.BrokerClaimReference;
                claim["statuscode"] = claimAttribute.claimStatus;
                if (claimAttribute.excess != null)
                    claim["new_excessamount"] = new Money(claimAttribute.excess.Value);
                                
                var createdClaimId = svc.Create(claim);

                return createdClaimId;
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Claim Class - CreateClaim Method", new Exception(ex.Message));
            }
        }
        #endregion
    }
}
