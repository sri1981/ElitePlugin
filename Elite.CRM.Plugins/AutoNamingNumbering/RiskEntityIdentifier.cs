using Elite.CRM.Plugins.Entities;
using Elite.CRM.Plugins.ErrorHandling;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.AutoNamingNumbering
{
    /// <summary>
    /// This plug-in creates a risk identifier for individual risk entities (e.g. new_pet) 
    /// based on risk identifiers associated with a risk class.
    /// </summary>
    public class RiskEntityIdentifier : BasePlugin
    {
        public RiskEntityIdentifier(string unsecureConfig, string secureConfig)
            : base(unsecureConfig, secureConfig)
        {
            // creates/updates name for risk identifier
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_riskidentifier", AutoNameRiskIdentifierOnCreate);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Update, "new_riskidentifier", AutoNameRiskIdentifierOnUpdate);

            // creates a new_name for insured risk
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_insuredrisk", AutoNameInsuredRiskOnCreate);

            // updates risk identifier for related risk record, if it is dependent on policy(version) fields, such as policy holder
            RegisterEvent(PluginStage.PostOperation, PluginMessage.Update, "new_insuredrisk", UpdateRelatedRiskIdentifier);

            // policy version update from gizmo portal which actually create a auto-number of policy version
            RegisterEvent(PluginStage.PostOperation, PluginMessage.Update, "new_policy", FixInsuredRiskNameOnChangePolicyVersion);

            // creates/updates new_name for risk entities
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_pet", SetIdentifier);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_vehicle", SetIdentifier);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_gadget", SetIdentifier);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_home", SetIdentifier);
            RegisterEvent(PluginStage.PreOperation, PluginMessage.Create, "new_rental", SetIdentifier);
        }

        protected void AutoNameRiskIdentifierOnCreate(LocalPluginContext context)
        {
            var identifier = context.PluginExecutionContext.InputParameters["Target"] as Entity;

            var fieldMappingRef = identifier.GetAttributeValue<EntityReference>("new_field");
            if (fieldMappingRef == null)
                throw new InvalidPluginExecutionException("Risk identifier error: Field mapping cannot be empty.");

            var fieldMapping = context.OrganizationService.Retrieve(fieldMappingRef);

            var riskClassRef = identifier.GetAttributeValue<EntityReference>("new_riskclass");
            if (riskClassRef == null)
                throw new InvalidPluginExecutionException("Risk identifier error: Risk class cannot be empty.");

            var riskClass = context.OrganizationService.Retrieve(riskClassRef);

            var sequence = identifier.GetAttributeValue<int?>("new_sequence");
            if (sequence == null)
                sequence = 0;

            var riskClassName = riskClass.GetAttributeValue<string>("new_name");
            var fieldMappingName = fieldMapping.GetAttributeValue<string>("new_name");

            var name = "{0} - {1} - {2}".FormatWith(riskClassName, sequence, fieldMappingName);
            identifier["new_name"] = name;
        }

        protected void AutoNameRiskIdentifierOnUpdate(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            var preImage = context.PreImage;

            var fieldMappingRef = target.GetAttributeWithFallback<EntityReference>("new_field", preImage);
            if (fieldMappingRef == null)
                throw new InvalidPluginExecutionException("Risk identifier error: Field mapping cannot be empty.");

            var fieldMapping = context.OrganizationService.Retrieve(fieldMappingRef);

            var riskClassRef = target.GetAttributeWithFallback<EntityReference>("new_riskclass", preImage);
            if (riskClassRef == null)
                throw new InvalidPluginExecutionException("Risk identifier error: Risk class cannot be empty.");

            var riskClass = context.OrganizationService.Retrieve(riskClassRef);

            var sequence = target.GetAttributeWithFallback<int?>("new_sequence", preImage);
            if (sequence == null)
                sequence = 0;

            var riskClassName = riskClass.GetAttributeValue<string>("new_name");
            var fieldMappingName = fieldMapping.GetAttributeValue<string>("new_name");

            var name = "{0} - {1} - {2}".FormatWith(riskClassName, sequence, fieldMappingName);
            target["new_name"] = name;
        }

        #region Insured risk

        /// <summary>
        /// Fixes a missing policy version number on insured risks. Policy version might get auto-numbered after
        /// it's created, then we have to update insured risk name as well.
        /// </summary>
        /// <param name="ctx"></param>
        protected void FixInsuredRiskNameOnChangePolicyVersion(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            var postImage = context.PostImage;

            // only execute when name changes
            if (!target.Contains("new_name"))
                return;

            var policyNumber = target.GetAttributeValue<string>("new_name");
            
            // if there's nothing in a name, do nothing
            if (string.IsNullOrWhiteSpace(policyNumber))
                return;

            var policyVersion = new PolicyVersion(context.OrganizationService, context.TracingService, postImage);

            var insuredRisks = policyVersion.InsuredRisks;
            foreach (var insRisk in insuredRisks)
            {
                var insRiskName = insRisk.Entity.GetAttributeValue<string>("new_name");

                // name already starts with policy number, no need to update it
                if (insRiskName.StartsWith(policyNumber))
                    continue;

                // in this case, insured risk is missing policy number
                if (insRiskName.StartsWith(" - "))
                {
                    var updatedInsRisk = new Entity(insRisk.LogicalName) { Id = insRisk.Id };
                    updatedInsRisk["new_name"] = policyNumber + insRiskName;
                    context.OrganizationService.Update(updatedInsRisk);
                }
            }

        }

        /// <summary>
        /// Sets new_name of an insured risk based on parent policy version number and new_name of risk entity.
        /// Since new_name of risk entity is risk identifier, which might depend on insured risk and its policy, 
        /// it gets updated as well.
        /// </summary>
        /// <param name="context">Local plug-in context.</param>
        protected void AutoNameInsuredRiskOnCreate(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            var insuredRisk = new InsuredRisk(context.OrganizationService, context.TracingService, target);

            var policyVersion = insuredRisk.PolicyVersion;
            if (policyVersion == null)
                throw new InvalidPluginExecutionException("Policy version is not selected for this insured risk.");

            var riskClass = insuredRisk.RiskClass;
            if (riskClass == null)
                throw new InvalidPluginExecutionException("Risk class is not specified for insured risk.");

            if (string.IsNullOrEmpty(riskClass.InsuredRiskLookup))
                throw new InvalidPluginExecutionException("Risk class configuration error - risk lookup field name is empty.");

            string riskName = null;

            // if risk entity (e.g.) is included in create request, its risk identifier might need to get updated
            var riskIdentifiers = riskClass.Identifiers;

            if (!riskIdentifiers.Any())
            {
                var riskSubClass = insuredRisk.RiskSubClass;
                if (riskSubClass != null)
                    riskName = riskSubClass.Entity.GetAttributeValue<string>("new_name");
            }
            else if (target.Contains(riskClass.InsuredRiskLookup))
            {
                var riskEntityRef = target.GetAttributeValue<EntityReference>(riskClass.InsuredRiskLookup);
                var riskEntity = context.OrganizationService.Retrieve(riskEntityRef);

                var policyFolder = policyVersion.PolicyFolder;
                //if (policyFolder == null)
                //    throw new InvalidPluginExecutionException("Policy is not selected for policy version.");

                riskName = riskEntity.GetAttributeValue<string>("new_name");
                
                if (riskIdentifiers.Any())
                {
                    var identifier = GetRiskIdentifierString(context.OrganizationService, policyFolder == null ? null : policyFolder.Entity, insuredRisk.Entity, riskEntity, riskIdentifiers);
                    if (identifier != riskEntity.GetAttributeValue<string>("new_name"))
                    {
                        var updatedRiskEntity = new Entity(riskEntity.LogicalName);
                        updatedRiskEntity["new_name"] = identifier;
                        updatedRiskEntity.Id = riskEntity.Id;

                        context.OrganizationService.Update(updatedRiskEntity);
                    }

                    riskName = identifier;
                }
            }

            var name = "{0} - {1}".FormatWith(policyVersion.PolicyVersionNumber, riskName);
            target["new_name"] = name.LimitLength(250);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context">Local plug-in context.</param>
        protected void UpdateRelatedRiskIdentifier(LocalPluginContext context)
        {
            var postImage = context.PostImage;
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;

            var insuredRisk = new InsuredRisk(context.OrganizationService, context.TracingService, postImage);

            var riskClass = insuredRisk.RiskClass;
            if (riskClass == null)
                throw new InvalidPluginExecutionException("Risk class is not specified for insured risk.");

            if (string.IsNullOrEmpty(riskClass.InsuredRiskLookup))
                throw new InvalidPluginExecutionException("Risk class configuration error - risk lookup field name is empty.");

            // we update risk identifier of risk entity only if it was just attached to the insured risk.
            if (!target.Contains(riskClass.InsuredRiskLookup))
                return;

            var policyVersion = insuredRisk.PolicyVersion;
            if (policyVersion == null)
                throw new InvalidPluginExecutionException("Policy version is not selected for this insured risk.");

            var policyFolder = policyVersion.PolicyFolder;
            //if (policyFolder == null)
            //    throw new InvalidPluginExecutionException("Policy is not selected for policy version.");

            var riskEntityRef = insuredRisk.RiskEntityRef;
            if (riskEntityRef == null)
                return;

            var riskEntity = context.OrganizationService.Retrieve(riskEntityRef);

            var identifier = GetRiskIdentifierString(context.OrganizationService, policyFolder == null ? null : policyFolder.Entity, insuredRisk.Entity, riskEntity, riskClass.Identifiers);

            if (string.IsNullOrEmpty(identifier))
                return;

            // if identifier is different to current name, update risk entity
            if (identifier != riskEntity.GetAttributeValue<string>("new_name"))
            {
                var updatedRiskEntity = new Entity(riskEntity.LogicalName);
                updatedRiskEntity["new_name"] = identifier;
                updatedRiskEntity.Id = riskEntity.Id;

                context.OrganizationService.Update(updatedRiskEntity);
            }

            // always update insured risk, because it might have been initially created without risk entity identifier
            var insuredRiskName = "{0} - {1}".FormatWith(policyVersion.PolicyVersionNumber, identifier);
            var updatedInsuredRisk = new Entity("new_insuredrisk");
            updatedInsuredRisk["new_name"] = insuredRiskName.LimitLength(250);
            updatedInsuredRisk.Id = target.Id;

            context.OrganizationService.Update(updatedInsuredRisk);
        }

        #endregion

        #region Risk entities - identifiers

        protected void SetIdentifier(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            Entity preImage = null;

            if (context.PluginExecutionContext.MessageName == "Update")
                preImage = context.PreImage;

            // name was explicitly set by another code/plug-in, keep it as it is
            if (target.Contains("new_name"))
                return;

            // get risk class for a name of entity, for which is this plug-in currently firing
            var riskClassEntity = RiskClass.RetrieveForEntityName(context.OrganizationService, target.LogicalName);

            // if no risk class is defined do not generate name
            if (riskClassEntity == null)
                return;

            var riskClass = new RiskClass(context.OrganizationService, context.TracingService, riskClassEntity);
            var riskIdentifiers = riskClass.Identifiers;

            var identifier = GetRiskIdentifierString(context.OrganizationService, null, null, target, riskIdentifiers);

            // previous call returns null if if fails to create a name
            if (identifier != null)
                target["new_name"] = identifier;
        }

        private static string GetRiskIdentifierString(IOrganizationService svc, Entity policyFolder, Entity insuredRisk, Entity riskEntity, IEnumerable<Entity> riskIdentifiers)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNull(riskEntity, "riskEntity");
            ThrowIf.Argument.IsNull(riskIdentifiers, "riskIdentifiers");

            // collect all parts from risk identifier
            var nameParts = new List<string>();
            foreach (var identifier in riskIdentifiers)
            {
                var entityName = identifier.GetAttributeValue<AliasedValue>("new_fieldmapping.new_destinationentityschemaname").ToValue<string>();
                var fieldName = identifier.GetAttributeValue<AliasedValue>("new_fieldmapping.new_destinationfield").ToValue<string>();

                if (string.IsNullOrEmpty(entityName) || string.IsNullOrEmpty(fieldName))
                    continue;

                Entity entity = null;

                if (entityName == riskEntity.LogicalName)
                {
                    // pick field value from risk entity
                    entity = riskEntity;
                }
                else if (entityName == "new_insuredrisk" && insuredRisk != null)
                {
                    entity = insuredRisk;
                }
                else if (policyFolder != null)
                {
                    // all non-risk-entity fields are from policy holder, either account or contact
                    if (entityName == "contact") // policy holder contact
                    {
                        var contactRef = policyFolder.GetAttributeValue<EntityReference>("new_contact");
                        if (contactRef == null)
                            continue;

                        entity = svc.Retrieve(contactRef);
                    }
                    else if (entityName == "account") // policy holder account
                    {
                        var accountRef = policyFolder.GetAttributeValue<EntityReference>("new_account");
                        if (accountRef == null)
                            continue;

                        entity = svc.Retrieve(accountRef);
                    }
                }

                if (entity == null)
                    continue;

                var value = entity.Contains(fieldName) ? entity[fieldName] : null;

                if (value != null)
                {
                    if (value is EntityReference)
                        nameParts.Add((value as EntityReference).Name);
                    else
                        nameParts.Add(value.ToString());
                }
            }

            if (nameParts.Any())
                return string.Join(" - ", nameParts);

            return null;
        }

        #endregion

    }
}
