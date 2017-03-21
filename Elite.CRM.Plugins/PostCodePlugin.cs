using Elite.CRM.Plugins.Entities;
using Elite.CRM.Plugins.ErrorHandling;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins
{
    public class PostCodePlugin : BasePlugin
    {
        private string _serviceAccount;
        private string _servicePassword;
        private bool _serviceEnabled;

        public PostCodePlugin(string unsecureConfig, string secureConfig)
            : base(unsecureConfig, secureConfig)
        {
            var config = ParseConfig(unsecureConfig);

            if (config != null)
            {
                _serviceAccount = config["account"];
                _servicePassword = config["password"];
                
                _serviceEnabled = true;
                if (config.ContainsKey("enabled"))
                    _serviceEnabled = bool.Parse(config["enabled"]);
            }

            // normalize name
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_postalcode", NormalizePostalcodeForSearch);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Update, "new_postalcode", NormalizePostalcodeForSearch);

            RegisterEvent(PluginStage.PostOperation, PluginMessage.Create, "new_postalcode", CreateAddressesForPostalCode);
        }

        protected void NormalizePostalcodeForSearch(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;

            var postalCode = target.GetAttributeValue<string>("new_name");
            if (string.IsNullOrEmpty(postalCode))
                return;

            EntityReference countryRef = null;

            if (context.PluginExecutionContext.MessageName == "Create")
            {
                countryRef = target.GetAttributeValue<EntityReference>("new_country");
            }
            else if (context.PluginExecutionContext.MessageName == "Update")
            {
                var preImage = context.PreImage;
                countryRef = target.GetAttributeWithFallback<EntityReference>("new_country", preImage);
            }

            if (countryRef == null)
                throw new InvalidPluginExecutionException("Country cannot be empty for postal code.");

            var existing = context.OrganizationService.SearchPostalCode(postalCode, countryRef.Id, context.PluginExecutionContext.PrimaryEntityId);

            if (existing != null)
                throw new InvalidPluginExecutionException("Postal code '{0}' already exists in the system.".FormatWith(postalCode));

            target["new_codeforsearch"] = Utils.NormalizePostalCode(postalCode);
        }

        protected void CreateAddressesForPostalCode(LocalPluginContext context)
        {
            if (!_serviceEnabled)
                return;

            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;

            if (!target.Contains("new_country"))
                throw new InvalidPluginExecutionException("Postal code does not contain country.");

            var countryRef = target.GetAttributeValue<EntityReference>("new_country");
            var country = context.OrganizationService.Retrieve(countryRef);

            var countryCode = country.GetAttributeValue<string>("new_countrycode");
            
            // TODO is such check for United kingdom sufficient?
            if (countryCode == "GB" || countryCode == "UK")
            {
                CreateAddressesForUk(context.OrganizationService, target, _serviceAccount, _servicePassword);
            }
        }

        /// <summary>
        /// Calls UK specific postal code service to retrieve all possible addresses and creates them in CRM.
        /// </summary>
        /// <param name="svc"></param>
        /// <param name="postalCode"></param>
        private static void CreateAddressesForUk(IOrganizationService svc, Entity postalCode, string login, string password)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNull(postalCode, "postalCode");

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                throw new InvalidPluginExecutionException("Username or password not configured for postal code web service.");
            }

            var postalCodeValue = postalCode.GetAttributeValue<string>("new_name");
            if (string.IsNullOrEmpty(postalCodeValue))
                return;

            var postalCodeRef = postalCode.ToEntityReference();
            var countryRef = postalCode.GetAttributeValue<EntityReference>("new_country");

            UkAddress address;
            try
            {
                using (var client = CreateUkServiceClient())
                {
                    address = new UkAddress(client.getAddress(login, password, postalCodeValue));
                }
            }
            catch (CommunicationException)
            {
                // WCF exception - there's not much we can do, but we cannot prevent
                // creation of postal code, which is dependency for address creation and 
                // selling policies.

                // 12/05/2016 - changed back to throwing exception when calling web service fails
                //return;
                throw;
            }

            if (!string.IsNullOrEmpty(address.ErrorMessage))
                throw new InvalidPluginExecutionException("Post code service error: '{0}'".FormatWith(address.ErrorMessage));

            var county = FindOrCreateCounty(svc, countryRef, address.County);
            var countyRef = county.ToEntityReference();

            var city = FindOrCreateCity(svc, countyRef, address.City);
            var cityRef = city.ToEntityReference();

            // set normalized postal code from web service, also city
            var updatedPostCode = new Entity(postalCode.LogicalName) { Id = postalCode.Id };
            updatedPostCode["new_name"] = address.Postcode;
            updatedPostCode["new_city"] = cityRef;
            svc.Update(updatedPostCode);

            foreach (var premises in address.Premises)
            {
                var addrEntity = new Entity("new_address");

                addrEntity["new_addressname"] = premises.BuildingDetails;
                addrEntity["new_addressnumbertext"] = premises.Number;

                addrEntity["new_county"] = countyRef;
                addrEntity["new_country"] = countryRef;
                addrEntity["new_city"] = cityRef;

                addrEntity["new_street1"] = address.Street1;
                addrEntity["new_street2"] = address.Street2;
                addrEntity["new_street3"] = address.Street3;

                addrEntity["new_addressorigin"] = AddressOrigin.PostalCodeSoftware.ToOptionSet();
                addrEntity["new_postalcode"] = postalCodeRef;

                svc.Create(addrEntity);
            }

            // premises info might be empty, but we still need to create one address record for 'top level address'
            if (!address.Premises.Any())
            {
                var addrEntity = new Entity("new_address");

                addrEntity["new_county"] = countyRef;
                addrEntity["new_country"] = countryRef;
                addrEntity["new_city"] = cityRef;

                addrEntity["new_street1"] = address.Street1;
                addrEntity["new_street2"] = address.Street2;
                addrEntity["new_street3"] = address.Street3;

                addrEntity["new_addressorigin"] = AddressOrigin.PostalCodeSoftware.ToOptionSet();
                addrEntity["new_postalcode"] = postalCodeRef;

                svc.Create(addrEntity);
            }
        }

        private static Entity FindOrCreateCounty(IOrganizationService svc, EntityReference countryRef, string countyName)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNull(countryRef, "countryRef");
            ThrowIf.Argument.IsNullOrEmpty(countyName, "countyName");

            var countyQuery = new QueryExpression("new_county");

            countyQuery.Criteria.AddCondition("new_name", ConditionOperator.Equal, countyName);
            countyQuery.Criteria.AddCondition("new_country", ConditionOperator.Equal, countryRef.Id);

            var county = svc.RetrieveMultiple(countyQuery).Entities.FirstOrDefault();

            if (county != null)
                return county;

            county = new Entity("new_county");
            county["new_name"] = countyName;
            county["new_country"] = countryRef;
            county.Id = svc.Create(county);

            return county;
        }

        private static Entity FindOrCreateCity(IOrganizationService svc, EntityReference countyRef, string cityName)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNull(countyRef, "countyRef");
            ThrowIf.Argument.IsNullOrEmpty(cityName, "cityName");

            var cityQuery = new QueryExpression("new_city");

            cityQuery.Criteria.AddCondition("new_name", ConditionOperator.Equal, cityName);
            cityQuery.Criteria.AddCondition("new_countyid", ConditionOperator.Equal, countyRef.Id);

            var city = svc.RetrieveMultiple(cityQuery).Entities.FirstOrDefault();

            if (city != null)
                return city;

            city = new Entity("new_city");
            city["new_name"] = cityName;
            city["new_countyid"] = countyRef;
            city.Id = svc.Create(city);

            return city;
        }

        private static UkPostalCodeService.LookupSoapClient CreateUkServiceClient()
        {
            var binding = new System.ServiceModel.BasicHttpBinding();
            var address = new System.ServiceModel.EndpointAddress("http://ws1.postcodesoftware.co.uk/lookup.asmx");

            var client = new UkPostalCodeService.LookupSoapClient(binding, address);
            return client;
        }

        #region UK data classes

        private class UkAddress
        {
            private UkPostalCodeService.Address _svcAddress;

            public UkAddress(UkPostalCodeService.Address svcAddress)
            {
                _svcAddress = svcAddress;
            }

            public string Street1 { get { return _svcAddress.Address1; } }
            public string Street2 { get { return _svcAddress.Address2; } }
            public string Street3 { get { return _svcAddress.Address3; } }

            public string City { get { return _svcAddress.Town; } }
            public string County { get { return _svcAddress.County; } }
            public string Postcode { get { return _svcAddress.Postcode; } }

            private List<UkPremises> _premises;
            public IEnumerable<UkPremises> Premises
            {
                get
                {
                    if (string.IsNullOrEmpty(_svcAddress.PremiseData))
                        return Enumerable.Empty<UkPremises>();

                    if (_premises != null)
                        return _premises;

                    _premises = new List<UkPremises>();

                    var premiseItems = _svcAddress.PremiseData
                        .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                    // de-duplication of premises records, because we ignore differences in Organization name
                    foreach (var item in premiseItems)
                    {
                        var prem = new UkPremises(item);

                        if (!string.IsNullOrEmpty(prem.BuildingDetails) || !string.IsNullOrEmpty(prem.Number))
                        {
                            if (!_premises.Any(p => p.BuildingDetails == prem.BuildingDetails && p.Number == prem.Number))
                                _premises.Add(prem);
                        }
                    }

                    return _premises;
                }
            }

            public string ErrorMessage { get { return _svcAddress.ErrorMessage; } }
        }

        private class UkPremises
        {
            public string BuildingDetails { get; set; }
            public string Number { get; set; }

            public UkPremises(string premisesDef)
            {
                var parts = premisesDef.Split('|');
                if (parts.Length != 3)
                    throw new Exception("PostCode service error: Invalid premises data='{0}'. Exactly 3 parts expected.".FormatWith(premisesDef));

                // organization info is ignored
                BuildingDetails = parts[1];
                Number = parts[2];
            }
        }

        #endregion

    }
}
