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
    sealed class ClaimPayment
    {
        IOrganizationService svc;
        int? claimOrder;
        MappedRow mappedRow;
        EntityReference country;

        public ClaimPayment(IOrganizationService _svc, MappedRow _mappedRow, EntityReference _country, int? _claimOrder)
        {
            svc = _svc;
            mappedRow = _mappedRow;
            country = _country;
            claimOrder = _claimOrder;
        }

        public void CreateOrUpdatePayment(Guid createdClaimId, Entity policy)
        {
            ThrowIf.Argument.IsNull(createdClaimId, "createdClaimId");
            ThrowIf.Argument.IsNull(mappedRow, "mappedRow");

            try
            {
                Entity claimPayment = new Entity("new_payment");

                var retrievedPaymentOrder = mappedRow.Attributes.ForClaimOrder(claimOrder)
                             .Where(c => c.TemplateColumn.ClaimPaymentOrder != null)
                             .Select(c => c.TemplateColumn.ClaimPaymentOrder)
                             .Distinct();

                foreach (var paymentOrder in retrievedPaymentOrder)
                {
                    var createdClaimPaymentId = CreatePayment(createdClaimId, paymentOrder, policy);

                    if (createdClaimPaymentId != Guid.Empty)
                    {
                        var paymentContact = mappedRow.Attributes.ForClaimOrder(claimOrder).ForPaymentOrder(paymentOrder);
                        if(paymentContact.Count() != 0)
                            CreateOrUpdatePartyRecipient(paymentContact, svc.Retrieve("new_payment", createdClaimPaymentId, new ColumnSet(true)));
                    }
                    #region commentedcode
                    //var paymentFields = mappedRow.Attributes.ForEntity("new_payment").ForClaimOrder(claimOrder).ForPaymentOrder(paymentOrder);

                    //var paidAmount = paymentFields.ForAttribute("new_amount").FirstOrDefault().AsDecimal(); //.Where(c => c.AttributeName == "new_amount").Select(c => c.ConvertedValue).FirstOrDefault();

                    //if (paidAmount != null && paidAmount > 0)
                    //{
                    //    if (paymentFields.Where(c => c.AttributeName == "new_paymenttype").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                    //        claimPayment["new_paymenttype"] = paymentFields.Where(c => c.AttributeName == "new_paymenttype").Select(c => c.ConvertedValue).FirstOrDefault();

                    //    if (paymentFields.Where(c => c.AttributeName == "new_paymentmethod").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                    //        claimPayment["new_paymentmethod"] = paymentFields.Where(c => c.AttributeName == "new_paymentmethod").Select(c => c.ConvertedValue).FirstOrDefault();

                    //    if (paymentFields.Where(c => c.AttributeName == "new_paymentrecipient").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                    //        claimPayment["new_paymentrecipient"] = paymentFields.Where(c => c.AttributeName == "new_paymentrecipient").Select(c => c.ConvertedValue).FirstOrDefault();

                    //    if (paymentFields.Where(c => c.AttributeName == "new_paymentclass").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                    //        claimPayment["new_paymentclass"] = paymentFields.Where(c => c.AttributeName == "new_paymentclass").Select(c => c.ConvertedValue).FirstOrDefault();

                    //    if (paymentFields.Where(c => c.AttributeName == "statuscode").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                    //        claimPayment["statuscode"] = paymentFields.Where(c => c.AttributeName == "statuscode").Select(c => c.ConvertedValue).FirstOrDefault();

                    //    if (createdClaimId != Guid.Empty)
                    //        claimPayment["new_claim"] = new EntityReference("new_claim", createdClaimId);

                    //    claimPayment["new_amount"] = paymentFields.Where(c => c.AttributeName == "new_amount").FirstOrDefault().ConvertedValue;

                    //    if (paymentFields.Where(c => c.AttributeName == "new_date").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                    //        claimPayment["new_date"] = paymentFields.Where(c => c.AttributeName == "new_date").FirstOrDefault().ConvertedValue;

                    //    if (paymentFields.Where(c => c.AttributeName == "new_recipientroleinpayment").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                    //        claimPayment["new_recipientroleinpayment"] = paymentFields.Where(c => c.AttributeName == "new_recipientroleinpayment").FirstOrDefault().ConvertedValue;

                    //    var createdClaimPaymentId = svc.Create(claimPayment);

                    //    if (createdClaimPaymentId != Guid.Empty)
                    //    {
                    //        var paymentContact = mappedRow.Attributes.ForClaimOrder(claimOrder).ForPaymentOrder(paymentOrder);
                    //        CreateOrUpdatePartyRecipient(paymentContact, svc.Retrieve("new_payment", createdClaimPaymentId, new ColumnSet(true)));
                    //    }
                    //}
                    #endregion
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("ClaimPayment Class - CreateOrUpdatePayment method", new Exception(ex.Message));
            }
        }

        #region CreatePayment
        private Guid CreatePayment(Guid createdClaimId, int? paymentOrder, Entity policy)
        {
            try
            {
                Entity claimPayment = new Entity("new_payment");

                Guid createdClaimPaymentId = Guid.Empty;

                var defaultReceipientRole = svc.RetrieveMultipleByName("new_roletype", "PolicyHolder").FirstOrDefault();

                var paymentFields = mappedRow.Attributes.ForEntity("new_payment").ForClaimOrder(claimOrder).ForPaymentOrder(paymentOrder);

                var paidAmount = paymentFields.ForAttribute("new_amount").FirstOrDefault().AsDecimal(); //.Where(c => c.AttributeName == "new_amount").Select(c => c.ConvertedValue).FirstOrDefault();

                if (paidAmount != null && paidAmount > 0)
                {
                    if (paymentFields.Where(c => c.AttributeName == "new_paymenttype").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                        claimPayment["new_paymenttype"] = paymentFields.Where(c => c.AttributeName == "new_paymenttype").Select(c => c.ConvertedValue).FirstOrDefault();

                    if (paymentFields.Where(c => c.AttributeName == "new_paymentmethod").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                        claimPayment["new_paymentmethod"] = paymentFields.Where(c => c.AttributeName == "new_paymentmethod").Select(c => c.ConvertedValue).FirstOrDefault();

                    if (paymentFields.Where(c => c.AttributeName == "new_paymentrecipient").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                        claimPayment["new_paymentrecipient"] = paymentFields.Where(c => c.AttributeName == "new_paymentrecipient").Select(c => c.ConvertedValue).FirstOrDefault();

                    if (paymentFields.Where(c => c.AttributeName == "new_paymentclass").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                        claimPayment["new_paymentclass"] = paymentFields.Where(c => c.AttributeName == "new_paymentclass").Select(c => c.ConvertedValue).FirstOrDefault();

                    if (paymentFields.Where(c => c.AttributeName == "statuscode").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                        claimPayment["statuscode"] = paymentFields.Where(c => c.AttributeName == "statuscode").Select(c => c.ConvertedValue).FirstOrDefault();

                    if (createdClaimId != Guid.Empty)
                        claimPayment["new_claim"] = new EntityReference("new_claim", createdClaimId);

                    claimPayment["new_amount"] = paymentFields.Where(c => c.AttributeName == "new_amount").FirstOrDefault().ConvertedValue;

                    if (paymentFields.Where(c => c.AttributeName == "new_date").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                        claimPayment["new_date"] = paymentFields.Where(c => c.AttributeName == "new_date").FirstOrDefault().ConvertedValue;
                    else
                        claimPayment["new_date"] = DateTime.Now;

                    if (paymentFields.Where(c => c.AttributeName == "new_recipientroleinpayment").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                        claimPayment["new_recipientroleinpayment"] = paymentFields.Where(c => c.AttributeName == "new_recipientroleinpayment").FirstOrDefault().ConvertedValue;
                    else
                        claimPayment["new_recipientroleinpayment"] = defaultReceipientRole.ToEntityReference();

                    if (paymentFields.Where(c => c.AttributeName == "new_paymentindicator").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                        claimPayment["new_paymentindicator"] = paymentFields.Where(c => c.AttributeName == "new_paymentindicator").Select(c => c.ConvertedValue).FirstOrDefault();

                    if (paymentFields.Where(c => c.AttributeName == "new_paymentrecipientcontact").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                        claimPayment["new_paymentrecipientcontact"] = paymentFields.Where(c => c.AttributeName == "new_paymentrecipientcontact").Select(c => c.ConvertedValue).FirstOrDefault();
                    else
                        claimPayment["new_paymentrecipientcontact"] = policy.GetAttributeValue<EntityReference>("new_contact");

                    if (paymentFields.Where(c => c.AttributeName == "new_accountid").Select(c => c.ConvertedValue).FirstOrDefault() != null)
                        claimPayment["new_accountid"] = paymentFields.Where(c => c.AttributeName == "new_accountid").Select(c => c.ConvertedValue).FirstOrDefault();
                    else
                        claimPayment["new_accountid"] = policy.GetAttributeValue<EntityReference>("new_broker");

                    createdClaimPaymentId = svc.Create(claimPayment);
                }

                return createdClaimPaymentId;
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("ClaimPayment Class - CreatePayment method", new Exception(ex.Message));
            }
        }
        #endregion

        #region CreateOrUpdatePartyRecipient
        /// <summary>
        /// Creates the or update party recipient.
        /// </summary>
        /// <param name="paymentContact">The payment contact.</param>
        /// <param name="createdPayment">The created payment.</param>
        /// <exception cref="InvalidPluginExecutionException">
        /// new Exception(No contact or account provided for payment)
        /// or
        /// ClaimPayment Class - CreateOrUpdatePayment method
        /// </exception>
        /// <exception cref="Exception">
        /// No contact or account provided for payment
        /// or
        /// </exception>
        public void CreateOrUpdatePartyRecipient(IEnumerable<MappedAttribute> paymentContact, Entity createdPayment)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNull(paymentContact, "paymentContact");
            ThrowIf.Argument.IsNull(createdPayment, "createdPayment");
            ThrowIf.Argument.IsNull(country, "country");

            try
            {
                var paymentToFields = paymentContact.Where(c => c.EntityName != "new_payment");

                var accountNameField = paymentToFields.Where(c => c.AttributeName == "name").Select(c => c.TemplateColumn.ColumnLabel).FirstOrDefault();

                var contactFirstNameField = paymentToFields.Where(c => c.AttributeName == "firstname").Select(c => c.TemplateColumn.ColumnLabel).FirstOrDefault();

                var contactLastNameField = paymentToFields.Where(c => c.AttributeName == "lastname").Select(c => c.TemplateColumn.ColumnLabel).FirstOrDefault();

                var accountName = paymentToFields.Where(c => c.AttributeName == "name").Select(c => c.Value).FirstOrDefault();

                var contactFirstName = paymentToFields.Where(c => c.AttributeName == "firstname").Select(c => c.Value).FirstOrDefault();

                var contactLastName = paymentToFields.Where(c => c.AttributeName == "lastname").Select(c => c.Value).FirstOrDefault();

                if (accountName == null)
                {
                    if (contactFirstName == null && contactLastName == null)
                        return;
                        //throw new InvalidPluginExecutionException(accountNameField + "-" + contactFirstNameField + "-" + contactLastNameField, new Exception("No contact or account provided for payment"));
                }

                var partyRef = svc.ProcessParty(paymentToFields, country);

                if (partyRef.LogicalName == "contact")
                    createdPayment["new_paymentrecipientcontact"] = partyRef;
                if (partyRef.LogicalName == "account")
                    createdPayment["new_paymentrecipientcompany"] = partyRef;

                svc.Update(createdPayment);
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("ClaimPayment Class - CreateOrUpdatePartyRecipient method" + "-" + ex.InnerException.Message);
            }
        }
        #endregion

        public void UpdateClaimPaymentStatus(Guid createdClaimId)
        {
            var createdPayments = svc.RetrieveClaimPaymentOrRecovery("new_payment", createdClaimId);

            if(createdPayments != null)
            {
                createdPayments["new_paymentclass"] = new OptionSetValue(100000001);
                svc.Update(createdPayments);
                var transaction = createdPayments.GetAttributeValue<EntityReference>("new_claimtransaction");
                var retrievedTransaction = svc.Retrieve(transaction.LogicalName, transaction.Id, new ColumnSet(true));
                retrievedTransaction["new_claimtransactiontype"] = new OptionSetValue(100000004);
                svc.Update(retrievedTransaction);
            }
        }
    }
}
