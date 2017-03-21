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

namespace Elite.CRM.Plugins.Entities
{
    class ClaimRecovery
    {
        IOrganizationService svc;
        int? claimOrder;
        MappedRow mappedRow;
        EntityReference country;

        public ClaimRecovery(IOrganizationService _svc, MappedRow _mappedRow, EntityReference _country, int? _claimOrder)
        {
            svc = _svc;
            mappedRow = _mappedRow;
            country = _country;
            claimOrder = _claimOrder;
        }

        public void CreateOrUpdateRecovery(Guid createdClaimId, Entity policy)
        {
            try
            {
                ThrowIf.Argument.IsNull(svc, "svc");
                ThrowIf.Argument.IsNull(createdClaimId, "createdClaimId");

                Entity claimRecovery = new Entity("new_claimrecovery");

                var retreievedRecoveryOrder = mappedRow.Attributes.ForClaimOrder(claimOrder)
                    .Where(c => c.TemplateColumn.ClaimRecoveryOrder != null)
                    .Select(c => c.TemplateColumn.ClaimRecoveryOrder)
                    .Distinct();

                foreach (var recoveryOrder in retreievedRecoveryOrder)
                {
                    var recoveryFields = mappedRow.Attributes.ForEntity("new_claimrecovery").ForClaimOrder(claimOrder).ForRecoveryOrder(recoveryOrder);

                    if (recoveryFields.Count() == 0)
                        return;

                    var recoveryAmount = recoveryFields.ForAttribute("new_recoveryamount").FirstOrDefault().AsDecimal(); //.Where(c => c.AttributeName == "new_recoveryamount").FirstOrDefault().ConvertedValue;

                    if (recoveryAmount != null && recoveryAmount > 0)
                    {
                        if (recoveryFields.Where(c => c.AttributeName == "new_recoverymethod").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                            claimRecovery["new_recoverymethod"] = recoveryFields.Where(c => c.AttributeName == "new_recoverymethod").Select(c => c.ConvertedValue).FirstOrDefault();
                        
                        if (recoveryFields.Where(c => c.AttributeName == "new_recoverytype").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                            claimRecovery["new_recoverytype"] = recoveryFields.Where(c => c.AttributeName == "new_recoverytype").Select(c => c.ConvertedValue).FirstOrDefault();

                        if (recoveryFields.Where(c => c.AttributeName == "new_recoveryclass").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                            claimRecovery["new_recoveryclass"] = recoveryFields.Where(c => c.AttributeName == "new_recoveryclass").Select(c => c.ConvertedValue).FirstOrDefault();

                        if (recoveryFields.Where(c => c.AttributeName == "new_recoveryamount").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                            claimRecovery["new_recoveryamount"] = recoveryFields.Where(c => c.AttributeName == "new_recoveryamount").FirstOrDefault().ConvertedValue;

                        if (recoveryFields.Where(c => c.AttributeName == "new_recoverydate").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                            claimRecovery["new_recoverydate"] = recoveryFields.Where(c => c.AttributeName == "new_recoverydate").FirstOrDefault().ConvertedValue;
                        else
                            claimRecovery["new_recoverydate"] = DateTime.Now;

                        if (recoveryFields.Where(c => c.AttributeName == "new_recoveryreference").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                            claimRecovery["new_recoveryreference"] = recoveryFields.Where(c => c.AttributeName == "new_recoveryreference").FirstOrDefault().ConvertedValue;

                        if (recoveryFields.Where(c => c.AttributeName == "new_recoveryissuingcontact").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                            claimRecovery["new_recoveryissuingcontact"] = recoveryFields.Where(c => c.AttributeName == "new_recoveryissuingcontact").FirstOrDefault().ConvertedValue;
                        else
                            claimRecovery["new_recoveryissuingcontact"] = policy.GetAttributeValue<EntityReference>("new_contact");

                        if (recoveryFields.Where(c => c.AttributeName == "new_recoveryfromcompany").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                            claimRecovery["new_recoveryfromcompany"] = recoveryFields.Where(c => c.AttributeName == "new_recoveryfromcompany").FirstOrDefault().ConvertedValue;
                        else
                            claimRecovery["new_recoveryfromcompany"] = policy.GetAttributeValue<EntityReference>("new_broker");

                        claimRecovery["new_claim"] = new EntityReference("new_claim", createdClaimId);

                        var createdClaimRecoveryId = svc.Create(claimRecovery);

                        if(createdClaimRecoveryId != null)
                        {
                            var recoveryContact = mappedRow.Attributes.ForClaimOrder(claimOrder).ForRecoveryOrder(recoveryOrder);
                            CreateOrUpdatePartyRecipient(recoveryContact, svc.Retrieve("new_claimrecovery", createdClaimRecoveryId, new ColumnSet(true)));
                        }
                    }
                }

                //return svc.Retrieve("new_claimrecovery", createdClaimRecoveryId, new ColumnSet(true));
            }
            catch(Exception ex)
            {
                throw new InvalidPluginExecutionException("ClaimRecovery Class", new Exception(ex.Message));
            }
        }

        //public void UpdateClaim(Entity retrievedClaim)
        //{
        //    var incurred = retrievedClaim.GetAttributeValue<Money>("new_incurred").Value;
        //    decimal recoveryOutstanding = 0;
        //    decimal recoveryAmount = 0;

        //    if (retrievedClaim.Contains("new_outstanding"))
        //        recoveryOutstanding = retrievedClaim.GetAttributeValue<Money>("new_outstanding").Value;

        //    //if (claimRecovery.Contains("new_recoveryamount"))
        //    //    recoveryAmount = claimRecovery.GetAttributeValue<Money>("new_recoveryamount").Value;

        //    recoveryOutstanding = recoveryOutstanding - recoveryAmount;

        //    if (recoveryOutstanding < 0)
        //        recoveryOutstanding = 0;

        //    var retrievedClaimRecovery = svc.RetrieveClaimTransactionsorPayments("new_claimrecovery", retrievedClaim.Id);

        //    decimal totalRecovered = 0;

        //    foreach (var recovered in retrievedClaimRecovery)
        //    {
        //        if (recovered.Contains("new_recoveryamount"))
        //        {
        //            totalRecovered += (decimal)recovered.GetAttributeValue<Money>("new_recoveryamount").Value;
        //        }
        //    }

        //    decimal recoveries = 0;

        //    recoveries = recoveryOutstanding + totalRecovered;

        //    //Entity claim = new Entity("new_claim");
        //    //claim.Id = claimId;
        //    claim["new_recovered"] = new Money(totalRecovered);

        //    if (recoveries >= 0)
        //        claim["new_recoveries"] = new Money(recoveries);

        //    if (recoveryOutstanding >= 0)
        //        claim["new_outstanding"] = new Money(recoveryOutstanding); // new Money(recoveryOutstanding - (decimal)recoveryAmount.Value);

        //    if (incurred - recoveries > 0)
        //        claim["new_incurred"] = new Money(incurred - recoveries);
        //    else
        //        claim["new_incurred"] = new Money(0);

        //    context.OrganizationService.Update(claim);
        //}

        #region CreateOrUpdatePartyRecipient
        public void CreateOrUpdatePartyRecipient(IEnumerable<MappedAttribute> recoveryContact, Entity createdClaimRecovery)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNull(recoveryContact, "paymentContact");
            ThrowIf.Argument.IsNull(createdClaimRecovery, "createdPayment");
            ThrowIf.Argument.IsNull(country, "country");

            try
            {
                var recoveryToFields = recoveryContact
                    .Where(c => c.EntityName != "new_claimrecovery");

                var accountNameField = recoveryToFields
                    .Where(c => c.AttributeName == "name")
                    .Select(c => c.TemplateColumn.ColumnLabel)
                    .FirstOrDefault();

                var contactFirstNameField = recoveryToFields
                    .Where(c => c.AttributeName == "firstname")
                    .Select(c => c.TemplateColumn.ColumnLabel)
                    .FirstOrDefault();

                var contactLastNameField = recoveryToFields
                    .Where(c => c.AttributeName == "lastname")
                    .Select(c => c.TemplateColumn.ColumnLabel)
                    .FirstOrDefault();

                var accountName = recoveryToFields
                    .Where(c => c.AttributeName == "name")
                    .Select(c => c.Value).FirstOrDefault();

                var contactFirstName = recoveryToFields
                    .Where(c => c.AttributeName == "firstname")
                    .Select(c => c.Value).FirstOrDefault();

                var contactLastName = recoveryToFields
                    .Where(c => c.AttributeName == "lastname")
                    .Select(c => c.Value).FirstOrDefault();

                if (accountName == null)
                {
                    if (contactFirstName == null && contactLastName == null)
                        return;
                        //throw new InvalidPluginExecutionException(accountNameField + "-" + contactFirstNameField + "-" + contactLastNameField, new Exception("No contact or account provided for Recovery"));
                }

                var partyRef = svc.ProcessParty(recoveryToFields, country);

                if (partyRef.LogicalName == "contact")
                    createdClaimRecovery["new_recoveryfromcontact"] = partyRef;
                if (partyRef.LogicalName == "account")
                    createdClaimRecovery["new_recoveryfromcompany"] = partyRef;

                svc.Update(createdClaimRecovery);
            }
            catch(Exception ex)
            {
                throw new InvalidPluginExecutionException(ex.InnerException.Message);
            }
        }
        #endregion

        public void UpdateClaimRecoveryStatus(Guid createdClaimId)
        {
            var createdRecovery = svc.RetrieveClaimPaymentOrRecovery("new_claimrecovery", createdClaimId);

            if (createdRecovery != null)
            {
                createdRecovery["new_recoveryclass"] = new OptionSetValue(100000001);
                svc.Update(createdRecovery);
                var transaction = createdRecovery.GetAttributeValue<EntityReference>("new_claimtransaction");
                if (transaction != null)
                {
                    var retrievedTransaction = svc.Retrieve(transaction.LogicalName, transaction.Id, new ColumnSet(true));
                    retrievedTransaction["new_claimtransactiontype"] = new OptionSetValue(100000008);
                    svc.Update(retrievedTransaction);
                }
            }
        }
    }
}
