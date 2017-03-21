using Elite.CRM.Plugins.ErrorHandling;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins
{
    public class ContactDuplicateCheckPlugin : BasePlugin
    {
        public ContactDuplicateCheckPlugin(string unsecureConfig, string secureConfig)
            : base(unsecureConfig, secureConfig)
        {
            // registering event guards against unintended execution in incorrect entity/message/stage
            //RegisterEvent(PluginStage.PostOperation, PluginMessage.Create, "contact", OnCreateDuplicateCheck);
            //RegisterEvent(PluginStage.PostOperation, PluginMessage.Update, "contact", OnUpdateDuplicateCheck);
        }

        protected void OnCreateDuplicateCheck(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            var duplicates = CheckForDuplicateContact(context.OrganizationService, target);
            
            if (duplicates.Count() == 0 || duplicates.Count() == 1 && duplicates.FirstOrDefault().Id == target.Id)
            {
                // found nothing, or only myself
                return;
            }
            else
            {
                throw new InvalidPluginExecutionException("Cannot create a contact because duplicate contact already exists in CRM.");
            }
        }

        protected void OnUpdateDuplicateCheck(LocalPluginContext context)
        {
            var postImage = context.PostImage;
            var duplicates = CheckForDuplicateContact(context.OrganizationService, postImage);

            if (duplicates.Count() == 0 || duplicates.Count() == 1 && duplicates.FirstOrDefault().Id == postImage.Id)
            {
                // found nothing, or only myself
                return;
            }
            else
            {
                throw new InvalidPluginExecutionException("Cannot update a contact because it would become a duplicate of a different contact.");
            }
        }

        private static IEnumerable<Entity> CheckForDuplicateContact(IOrganizationService svc, Entity contact)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNull(contact, "contact");

            var firstName = contact.GetAttributeValue<string>("firstname");
            var lastName = contact.GetAttributeValue<string>("lastname");

            var postalCodeRef = contact.GetAttributeValue<EntityReference>("new_postalcode");
            var postalCode = svc.Retrieve(postalCodeRef);
            var postalCodeString = postalCode.GetAttributeValue<string>("new_name");

            var dateOfBirth = contact.GetAttributeValue<DateTime?>("birthdate");
            if (dateOfBirth != null)
                dateOfBirth = dateOfBirth.Value.ToLocalTime();

            var email = contact.GetAttributeValue<string>("emailaddress1");

            var mobile = contact.GetAttributeValue<string>("mobilephone");
            var niNumber = contact.GetAttributeValue<string>("new_clientninumber");

            return svc.RetrieveContactByName(firstName, lastName, postalCodeString, dateOfBirth, email, mobile, niNumber);
        }

    }
}
