using Elite.CRM.Plugins.Entities;
using Elite.CRM.Plugins.ErrorHandling;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.BordereauImport
{
    class PolicyMapperDefaults
    {
        public EntityReference Country { get; set; }
        public EntityReference Currency { get; set; }
        public EntityReference Product { get; set; }
        public EntityReference Broker { get; set; }
        public EntityReference BordereauProcess { get; set; }
        public EntityReference MonthlyBordereau { get; set; }
    }

    /// <summary>
    /// Class responsible for creating policy version based on data from Bx row.
    /// </summary>
    class PolicyMapper
    {
        /// <summary>
        /// Default implementation of ITracingService. It is used to avoid null-checks whenever this class writes 
        /// to trace log.
        /// </summary>
        private class EmptyTrace : ITracingService
        {
            public void Trace(string format, params object[] args) // no-op
            { }
        }

        private IOrganizationService _svc;
        private ITracingService _trace = new EmptyTrace();

        private IEnumerable<MappedAttribute> _attrs;
        private BordereauTemplate _template;
        private PolicyMapperDefaults _defaults = new PolicyMapperDefaults();

        public string PolicyNumber
        {
            get
            {
                var numberAttr = _attrs
                    .ForEntity("new_policy")
                    .ForAttribute("new_name")
                    .FirstOrDefault();

                return numberAttr == null ? null : numberAttr.AsString();
            }
        }

        public string PolicyHolderEntityName
        {
            get
            {
                var policyHolder = _attrs
                    .ForRoleType(null)
                    .ContactOrAccount();

                if (policyHolder == null)
                    throw BordereauException.DataError("PolicyHolder details cannot be empty"); //new BordereauError(BordereauErrorType.BusinessError, "PolicyHolder details cannot be empty");
                return policyHolder;
            }
        }

        public PolicyMapper(IOrganizationService svc, MappedRow row, PolicyMapperDefaults defaults = null)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNull(row, "row");

            _svc = svc;
            _template = row.Template;
            _attrs = row.Attributes;

            if (defaults != null)
                _defaults = defaults;
        }

        /// <summary>
        /// Setter method for ITracingService, in case diagnostic messages are needed.
        /// </summary>
        /// <param name="trace">ITracingService implementation to use for tracing.</param>
        public void SetTracingService(ITracingService trace)
        {
            if (trace != null)
                _trace = trace;
        }

        public IEnumerable<BordereauError> Validate()
        {
            var errList = new List<BordereauError>();

            //if(this.PolicyHolderEntityName == null)
            //    errList.AddRange()
            // policyholder and address
            errList.AddRange(_attrs.ForRoleType(null).ForEntity(this.PolicyHolderEntityName).Validate());
            errList.AddRange(_attrs.ForRoleType(null).ForEntity("new_address").Validate());

            // policy folder and policy version
            errList.AddRange(_attrs.ForEntity("new_policyfolder").Validate());
            errList.AddRange(_attrs.ForEntity("new_policy").Validate());

            // product 
            if (_defaults.Product == null)
            {
                errList.AddRange(_attrs.ForEntity("new_policyfolder").ForAttribute("new_product").Validate());
            }

            // roles
            foreach (var role in _template.UniqueRoleTypeIDs)
            {
                var attrsForRole = _attrs.ForRoleType(role);

                // validate address
                errList.AddRange(attrsForRole.ForEntity("new_address").Validate());

                // validate contact/account
                var partyName = attrsForRole.ContactOrAccount();
                errList.AddRange(attrsForRole.ForEntity(partyName).Validate());
            }

            var riskClass = _template.RiskClass;

            errList.AddRange(_attrs.ForEntity("new_insuredrisk").Validate());
            //errList.AddRange(_attrs.ForEntity(riskClass.RiskEntity).Validate());

            return errList;
        }

        public EntityReference ProcessPolicyHolder()
        {
            // default role party without any role is policyholder
            return _svc.ProcessParty(_attrs.ForRoleType(null), _defaults.Country);
            
        }

        public Entity CreatePolicyVersion(EntityReference policyHolder, EntityReference product)
        {
            var transactionAttr = _attrs
                    .ForEntity("new_policy")
                    .ForAttribute("new_transactiontype")
                    .FirstOrDefault();

            if (transactionAttr == null)
                throw BordereauException.TemplateError("Transaction type of Policy is not mapped in Bordereau template.");

            var transactionDateAttr = _attrs
                    .ForEntity("new_policy")
                    .ForAttribute("new_commencementofcover")
                    .FirstOrDefault();

            if (transactionDateAttr == null)
                throw BordereauException.TemplateError("Transaction date is not mapped in Bordereau template.");

            var transactionType = transactionAttr.AsOptionSet().ToEnum<PolicyVersionTransactionType>();
            var transactionDate = transactionDateAttr.AsDateTime();

            _trace.Trace("Searching for policy folder by policy number: '{0}'", this.PolicyNumber);
            var policyFolder = _svc.RetrieveMultipleByName("new_policyfolder", this.PolicyNumber)
                .FirstOrDefault();

            if (policyFolder == null)
                _trace.Trace("Policy with number '{0}' does not exist.", this.PolicyNumber);

            _trace.Trace("Creating risk identifier for current row data.");
            //var currentRiskIdentifier = CreateRiskIdentifier(riskClass);
            //_trace.Trace("Risk identifier: {0}", currentRiskIdentifier);

            // re-upload o same file/rows
            //   - same policy number 
            //   - same transaction type
            //   - same transaction effective date
            //   - same risk entity (e.g. vehicle)
            // result: ignore row, it should not be processed
            //_trace.Trace("Searching for existing policy version: re-upload of row.");
            //var previouslyUploaded = FindExistingPolicyVersion(transactionType, transactionDate.Value, currentRiskIdentifier);
            //if (previouslyUploaded != null)
            //{
            //    _trace.Trace("Policy with the same number and risk identifier already exists. Skipping the row.");
            //    return null;
            //}

            // 2. adding additional risk object to existing policy version
            //   - same policy number
            //   - same transaction type
            //   - same transaction effective date
            //   - different risk entity (e.g. vehicle)
            // result: pick only risk entity (and insured risk) attributes from row, create additional insured risk(s) for PV
            
            _trace.Trace("Searching for existing policy version: additional risk row.");
            var previouslyUploaded = FindExistingPolicyVersion(transactionType, transactionDate.Value);

            if (previouslyUploaded != null)
            {
                _trace.Trace("Policy with this number already exists."); //additional risk will be added based on this row.
                return null;
            }

            Entity policyVersion = null;

            if (transactionType == PolicyVersionTransactionType.Cancellation)
            {
                if (policyFolder != null)
                {
                    _trace.Trace("Cancellation: Updating policy folder with cancellation information.");
                    // update policy folder, simulate cancellation request,
                    // creation of policy version will do the rest
                    var policyFolderToUpdate = new Entity(policyFolder.LogicalName) { Id = policyFolder.Id };
                    policyFolderToUpdate["new_cancellationresponsible"] = PolicyCancellationResponsible.Broker.ToOptionSet();

                    _svc.Update(policyFolderToUpdate);
                }
            }

            if (previouslyUploaded == null)
            {
                _trace.Trace("Policy version was not previously uploaded. Creating new policy version.");
                // new policy version
                policyVersion = new Entity("new_policy");
                policyVersion["new_name"] = this.PolicyNumber;
                policyVersion["new_inputchannel"] = PolicyInputChannel.Import.ToOptionSet();
                policyVersion["new_broker"] = _defaults.Broker;

                // link policy version with current bordereau process
                policyVersion["new_bordereauxprocessid"] = _defaults.BordereauProcess;
                policyVersion["new_monthlybordereau"] = _defaults.MonthlyBordereau;

                policyVersion.UpdateWithAttributes(_attrs.ForEntity("new_policy"));

                _trace.Trace("- Setting policy holder.");
                // set policy holder of policy, based on entity name
                if (this.PolicyHolderEntityName == "account")
                    policyVersion["new_insuredid"] = policyHolder;
                else if (this.PolicyHolderEntityName == "contact")
                    policyVersion["new_insured_contact"] = policyHolder;

                _trace.Trace("- Setting product.");
                // set product
                policyVersion["new_productid"] = product;

                // set policy folder, if already exists
                if (policyFolder != null)
                    policyVersion["new_policy"] = policyFolder.ToEntityReference();

                _trace.Trace("- Creating policy record.");
                policyVersion.Id = _svc.Create(policyVersion);
                
                // retrieve policy version to get all updates done by plugin(s)
                _trace.Trace("- Retrieving policy with latest data.");
                policyVersion = _svc.Retrieve(policyVersion.ToEntityReference());
            }
            else
            {
                // policy version exists, but we should add insured risk/risk object
                policyVersion = previouslyUploaded;
            }

            // at this point, policy version, insured risks and covers exist in CRM
            // and can be processed further
            return policyVersion;
            #region TODO fix this!!!

            //if (transactionType == PolicyVersionTransactionType.NewPolicy)
            //{
            //    if (policyFolder != null)
            //    {
            //    }
            //    else
            //    {
            //        //var policyFolderWrapped = new Policy(context.OrganizationService, context.TracingService, policyFolder);
            //        //if (policyFolderWrapped.Versions.Any(version => version.TransactionType == PolicyVersionTransactionType.NewPolicy))
            //        //{
            //        //    // TODO log error for duplicate NewPolicy transaction?
            //        //    //allErrors.AddError(new BordereauError(policyFolderNumberAttr.TemplateColumn, BordereauErrorType.BusinessError, "'New Policy' transaction already exists."));
            //        //    continue;
            //        //}
            //    }
            //}
            //else if (policyFolder == null)
            //{
            //    //allErrors.AddError(new BordereauError(policyFolderNumberAttr.TemplateColumn, BordereauErrorType.BusinessError, policyFolderNumberAttr.Value, "Policy with number='{0}' does not exist.".FormatWith(policyFolderNumberAttr.Value)));
            //    //continue;
            //}
            #endregion
        }

        /// <summary>
        /// Ensures that risk entity (e.g. Vehicle) exists in CRM. Based on Risk Class identifiers, checks if 
        /// risk entity exists in CRM. If so, returns existing entity. Otherwise, creates new risk entity
        /// based on attributes.
        /// </summary>
        /// <param name="riskClass"></param>
        /// <returns></returns>
        public Entity ImportRiskEntity(RiskClass riskClass)
        {
            var riskEntityName = riskClass.RiskEntity;
            var riskEntityAttrs = _attrs.ForEntity(riskEntityName);

            var currentRowIdentifier = CreateRiskIdentifier(riskClass);
            if (currentRowIdentifier != null)
            {
                var existingRisks = _svc.RetrieveMultipleByName(riskEntityName, currentRowIdentifier);

                if (existingRisks.Any())
                    return existingRisks.FirstOrDefault();
            }

            var risk = new Entity(riskEntityName);
            risk.UpdateWithAttributes(riskEntityAttrs);

            risk.Id = _svc.Create(risk);
            return risk;
        }


        internal void CreateOptionalInsuredRisks()
        {
            //throw new NotImplementedException();
        }

        /// <summary>
        /// Updates insured risks of policy version based on risk subclass on template columns.
        /// </summary>
        /// <param name="policyVersion"></param>
        internal void UpdateInsuredRisks(Entity policyVersion)
        {
            var policyVersionWrapped = new PolicyVersion(_svc, _trace, policyVersion);
            var insuredRisks = policyVersionWrapped.InsuredRisks;

            var insuredRisksAttrGroups = _attrs
                .ForEntity("new_insuredrisk")
                .ExcludeAttributes("new_riskid") // exclude primary ID for risk
                .GroupBy(a => a.TemplateColumn.RiskSubClass);

            foreach (var attrGroup in insuredRisksAttrGroups)
            {
                var subClassRef = attrGroup.Key;

                if (subClassRef == null)
                {
                    // no subclass => all insured risks
                    // TODO implement
                }
                else
                {
                    var insRisk = insuredRisks
                        .FirstOrDefault(ir => ir.RiskSubClass.Id == subClassRef.Id);

                    if (insRisk == null)
                        continue;

                    var insRiskToUpdate = new Entity("new_insuredrisk") { Id = insRisk.Id };
                    insRiskToUpdate.UpdateWithAttributes(attrGroup);
                    _svc.Update(insRiskToUpdate);
                }
            }
        }

        // Document that this also processes address!
        public void AddRiskToPolicy(Entity policyVersion, Entity riskEntity, string lookupName)
        {
            var policyVersionWrapped = new PolicyVersion(_svc, _trace, policyVersion);
            // == scenarios ==
            // 1. first risk object
            //   - find all insured risks with risk obj. empty, set current risk entity
            // 2. additional risk object - !!!not implemented at the moment !!!
            //   - create insured risk for every risk object of product, add risk entity to them

            var insuredRisks = policyVersionWrapped.InsuredRisks;

            if (insuredRisks.All(ir => ir.RiskEntityRef == null))
            {
                var insuredRiskAddress = _svc.ProcessAddress(_attrs.ForEntity("new_address").ForAddressOf(AddressOf.InsuredRisk), _defaults.Country);

                EntityReference addressRef = null;
                if (insuredRiskAddress != null && insuredRiskAddress.Address != null)
                {
                    // empty GUID -> address record not created yet
                    if (insuredRiskAddress.Address.Id == Guid.Empty)
                        insuredRiskAddress.Address.Id = _svc.Create(insuredRiskAddress.Address);

                    addressRef = insuredRiskAddress.Address.ToEntityReference();
                }

                foreach (var ir in insuredRisks)
                {
                    var updatedInsuredRisk = new Entity(ir.LogicalName) { Id = ir.Id };
                    updatedInsuredRisk[lookupName] = riskEntity.ToEntityReference();

                    if (insuredRiskAddress != null)
                    {
                        updatedInsuredRisk["new_country"] = insuredRiskAddress.Country;
                        updatedInsuredRisk["new_postalcode"] = insuredRiskAddress.PostalCode;
                        updatedInsuredRisk["new_address"] = addressRef;
                    }

                    _svc.Update(updatedInsuredRisk);
                }
            }
        }

        public void AddRoleToPolicy(Guid roleTypeId, EntityReference policyFolderRef)
        {
            // all attributes for specific role ID (e.g. attrs for all drivers)
            var allRoleAttributes = _attrs.ForRoleType(roleTypeId);

            allRoleAttributes.ForEachRoleNumber((roleNum, roleAttrs) =>
            {
                // process all individual role numbers (e.g. driver 1)
                var partyRef = _svc.ProcessParty(roleAttrs, _defaults.Country);

                if (partyRef == null)
                    return;

                if (CheckExistingRole(policyFolderRef, partyRef, roleTypeId))
                    return;

                var roleInPolicy = new Entity("new_roleinpolicy");
                roleInPolicy["new_roletypeid"] = new EntityReference("new_roletype", roleTypeId);
                roleInPolicy["new_policy"] = policyFolderRef;

                roleInPolicy.UpdateWithAttributes(roleAttrs.ForEntity("new_roleinpolicy"), false);

                if (partyRef.LogicalName == "contact")
                    roleInPolicy["new_contactid"] = partyRef;
                else if (partyRef.LogicalName == "account")
                    roleInPolicy["new_accountid"] = partyRef;

                _svc.Create(roleInPolicy);
            });
        }

        /// <summary>
        /// Creates a risk identifier from imported data, if possible.
        /// </summary>
        /// <param name="riskClass"></param>
        /// <returns></returns>
        public string CreateRiskIdentifier(RiskClass riskClass)
        {
            var riskIdentifiers = riskClass.Identifiers;
            var parts = new List<string>();

            foreach (var identifier in riskIdentifiers)
            {
                var entityName = identifier.GetAttributeValue<AliasedValue>("new_fieldmapping.new_destinationentityschemaname").ToValue<string>();
                var fieldName = identifier.GetAttributeValue<AliasedValue>("new_fieldmapping.new_destinationfield").ToValue<string>();

                if (string.IsNullOrEmpty(entityName) || string.IsNullOrEmpty(fieldName))
                    continue;

                // special handling of new_address.new_name
                if (fieldName == "new_address")
                {
                    var insuredRiskAddressAttrs = _attrs
                        .ForEntity("new_address")
                        .ForAddressOf(AddressOf.InsuredRisk);

                    var addressName = StringifyAddress(insuredRiskAddressAttrs, _defaults.Country);
                    parts.Add(addressName);
                }
                else
                {
                    var identifierAttr = _attrs
                        .ForEntity(entityName)
                        .ForAttribute(fieldName)
                        .FirstOrDefault();

                    if (identifierAttr == null)
                        continue;

                    parts.Add(identifierAttr.Value);
                }
            }

            if (parts.Any())
                return string.Join(" - ", parts);
            else
                return null;
        }

        internal string StringifyAddress(IEnumerable<MappedAttribute> attributes, EntityReference defaultCountry)
        {
            Entity country = null;

            if (defaultCountry != null)
            {
                country = _svc.Retrieve(defaultCountry, "new_countrycode");
            }
            else
            {
                var countryAttr = attributes.ForAttribute("new_country").FirstOrDefault();
                if (countryAttr != null)
                    country = countryAttr.AsEntity(); // implement mapping by something else than country name
            }

            var nameParts = new List<string>();

            if (country != null)
                nameParts.Add(country.GetAttributeValue<string>("new_countrycode"));

            var postalCodeAttr = attributes.ForAttribute("new_postalcode").FirstOrDefault();
            if (postalCodeAttr != null && postalCodeAttr.HasValue)
                nameParts.Add(postalCodeAttr.AsString());

            var addressNameAttr = attributes.ForAttribute("new_addressname").FirstOrDefault();
            if (addressNameAttr != null && addressNameAttr.HasValue)
                nameParts.Add(addressNameAttr.AsString());

            var numberAttr = attributes.ForAttribute("new_addressnumbertext").FirstOrDefault();
            var street1Attr = attributes.ForAttribute("new_street1").FirstOrDefault();

            var numberAndStreet = "{0} {1}".FormatWith(numberAttr == null ? null : numberAttr.AsString(),
                                                       street1Attr == null ? null : street1Attr.AsString()).Trim();

            if (!string.IsNullOrEmpty(numberAndStreet))
                nameParts.Add(numberAndStreet);

            return string.Join(" - ", nameParts);
        }

        public Product ResolveProduct()
        {
            var productAttr = _attrs
                   .ForEntity("new_product")
                   .FirstOrDefault();

            // product in Bordereau mapping has priority over the one in Bordereau process
            if (productAttr != null)
            {
                if (!productAttr.HasValue)
                    throw BordereauException.DataError("Product information is missing.");

                var searchProductByField = productAttr.AttributeName;
                var searchProductByValue = productAttr.Value;

                var product = _svc.RetrieveMultipleByAttribute("new_product", searchProductByField, searchProductByValue).FirstOrDefault();

                if (product == null)
                {
                    throw BordereauException.DataError("Product not found for value '{0}'.".FormatWith(productAttr.Value));

                    //allErrors.AddError(new BordereauError(productAttr.TemplateColumn,
                    //    BordereauErrorType.BusinessError,
                    //    productAttr.Value,
                    //    );
                }

                return new Product(_svc, _trace, product);
            }
            else if (_defaults.Product != null)
            {
                return new Product(_svc, _trace, _svc.Retrieve(_defaults.Product));
            }
            else
            {
                throw BordereauException.TemplateError("Product information is missing.");
                //allErrors.AddError(new BordereauError(BordereauErrorType.BusinessError, "Product information is missing."));
            }
        }

        private Entity FindExistingPolicyVersion(PolicyVersionTransactionType transactionType, DateTime transactionDate, string riskIdentifier = null)
        {
            var policyQuery = new QueryExpression("new_policy");
            policyQuery.ColumnSet.AllColumns = true;

            policyQuery.Criteria.AddCondition("new_transactiontype", ConditionOperator.Equal, (int)transactionType);
            policyQuery.Criteria.AddCondition("new_commencementofcover", ConditionOperator.On, transactionDate);

            var policyLink = policyQuery.AddLink("new_policyfolder", "new_policy", "new_policyfolderid");
            policyLink.LinkCriteria.AddCondition("new_name", ConditionOperator.Equal, this.PolicyNumber);

            if (!string.IsNullOrEmpty(riskIdentifier))
            {
                var insRiskLink = policyQuery.AddLink("new_insuredrisk", "new_policyid", "new_policyid");
                // insured risk auto name is [policy version number] - [risk identifier]
                insRiskLink.LinkCriteria.AddCondition("new_name", ConditionOperator.EndsWith, " - " + riskIdentifier);
            }

            return _svc.RetrieveMultiple(policyQuery).Entities.FirstOrDefault();
        }

        private bool CheckExistingRole(EntityReference policyFolderRef, EntityReference partyRef, Guid roleTypeId)
        {
            var roleQuery = new QueryExpression("new_roleinpolicy");

            roleQuery.Criteria.AddCondition("new_policy", ConditionOperator.Equal, policyFolderRef.Id);
            roleQuery.Criteria.AddCondition("new_roletypeid", ConditionOperator.Equal, roleTypeId);

            if (partyRef.LogicalName == "account")
                roleQuery.Criteria.AddCondition("new_accountid", ConditionOperator.Equal, partyRef.Id);
            else if (partyRef.LogicalName == "contact")
                roleQuery.Criteria.AddCondition("new_contactid", ConditionOperator.Equal, partyRef.Id);

            roleQuery.TopCount = 1;
            return _svc.RetrieveMultiple(roleQuery).Entities.Count > 0;
        }

        //private EntityReference GetBxCancellationReason()
        //{
        //    var reasonQuery = new QueryExpression("new_policycancellationreason");
        //    reasonQuery.Criteria.AddCondition("", ConditionOperator.Equal, );

        //}
    }
}
