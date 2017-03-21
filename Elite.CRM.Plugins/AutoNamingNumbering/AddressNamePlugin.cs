using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.AutoNamingNumbering
{
    public class AddressNamePlugin : BasePlugin
    {
        public AddressNamePlugin(string unsecureConfig, string secureConfig)
            : base(unsecureConfig, secureConfig)
        {
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_address", UpdateAddressName);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Update, "new_address", UpdateAddressNameOnUpdate);
        }

        private void UpdateAddressNameOnUpdate(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            var preImage = context.PreImage;

            var countryRef = target.GetAttributeWithFallback<EntityReference>("new_country", preImage);
            if (countryRef == null)
                throw new InvalidPluginExecutionException("Country of an address record cannot be empty.");

            var nameParts = new List<string>();

            var country = context.OrganizationService.Retrieve(countryRef, "new_countrycode");
            nameParts.Add(country.GetAttributeValue<string>("new_countrycode"));

            var postalCodeRef = target.GetAttributeWithFallback<EntityReference>("new_postalcode", preImage);
            if (postalCodeRef != null)
            {
                var postalCode = context.OrganizationService.Retrieve(postalCodeRef, "new_name");
                nameParts.Add(postalCode.GetAttributeValue<string>("new_name"));
            }

            var addressName = target.GetAttributeWithFallback<string>("new_addressname", preImage);
            if (!string.IsNullOrEmpty(addressName))
                nameParts.Add(addressName);

            var numberAndStreet = "{0} {1}".FormatWith(target.GetAttributeWithFallback<string>("new_addressnumbertext", preImage),
                                                       target.GetAttributeWithFallback<string>("new_street1", preImage)).Trim();

            if (!string.IsNullOrEmpty(numberAndStreet))
                nameParts.Add(numberAndStreet);

            var name = string.Join(" - ", nameParts);
            target["new_name"] = name;
        }

        protected void UpdateAddressName(LocalPluginContext context)
        {
            var address = context.PluginExecutionContext.InputParameters["Target"] as Entity;

            var countryRef = address.GetAttributeValue<EntityReference>("new_country");
            if (countryRef == null)
                throw new InvalidPluginExecutionException("Country of an address record cannot be empty.");

            // name format: [country code] - 
            var nameParts = new List<string>();

            var country = context.OrganizationService.Retrieve(countryRef, "new_countrycode");
            nameParts.Add(country.GetAttributeValue<string>("new_countrycode"));

            var postalCodeRef = address.GetAttributeValue<EntityReference>("new_postalcode");
            if (postalCodeRef != null)
            {
                var postalCode = context.OrganizationService.Retrieve(postalCodeRef, "new_name");
                nameParts.Add(postalCode.GetAttributeValue<string>("new_name"));
            }

            var addressName = address.GetAttributeValue<string>("new_addressname");
            if (!string.IsNullOrEmpty(addressName))
                nameParts.Add(addressName);

            var numberAndStreet = "{0} {1}".FormatWith(address.GetAttributeValue<string>("new_addressnumbertext"),
                                                       address.GetAttributeValue<string>("new_street1")).Trim();

            if (!string.IsNullOrEmpty(numberAndStreet))
                nameParts.Add(numberAndStreet);

            var name = string.Join(" - ", nameParts);
            address["new_name"] = name;
        }
    }
}
