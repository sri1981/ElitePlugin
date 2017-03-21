using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elite.CRM.Plugins.Entities;
using Elite.CRM.Plugins.ErrorHandling;
using Microsoft.Xrm.Sdk;
using System.ServiceModel;
using Elite.CRM.Plugins;
using Microsoft.Xrm.Sdk.Query;

namespace Elite.CRM.Plugins.BordereauImport
{
    class ClaimAttribute
    {
        #region datamembers
        public MappedAttribute claimNotificationDate { get; set; }
        public DateTime lossDateField { get; set; }
        public MappedAttribute policyNumber { get; set; }
        public IEnumerable<Entity> policy { get; set; }
        public IList<Entity> lossType { get; set; }
        public Entity subPeril { get; set; }
        public Guid retrievedCoverId { get; set; }
        public Entity policyVersion { get; set; }
        public IList<Entity> insuredCover { get; set; }
        public IEnumerable<Entity> insuredRisk { get; set; }
        public Guid productId { get; set; }
        public string claimFolderReferenceValue { get; set; }
        public Guid brokerId { get; set; }
        public Guid bxprocessId { get; set; }
        public EntityReference premiumPaymentMethod { get; set; }
        public decimal? ClaimedAmount { get; set; }
        public decimal? Reserve { get; set; }
        public decimal? Incurred { get; set; }
        public string lossTypeDescription { get; set; }
        public string BrokerClaimReference { get; set; }
        public OptionSetValue claimStatus { get; set; }
        public decimal? excess { get; set; }
        #endregion
    }
    class MappedClaim
    {
        ClaimAttribute mapAttribute = new ClaimAttribute();
        
        #region MapClaim
        public List<Guid> MapClaim(IOrganizationService svc, MappedRow mappedRow, BordereauTemplate template, BordereauProcess bxProcess)
        {
            ThrowIf.Argument.IsNull(svc, "svc");
            ThrowIf.Argument.IsNull(mappedRow, "mappedRow");
            ThrowIf.Argument.IsNull(template, "template");
            ThrowIf.Argument.IsNull(bxProcess, "bxProcess");
            
            Guid createdClaimId = Guid.Empty;
            List<Guid> createdClaim = new List<Guid>();
            try
            {
                //Get the template columns
                var templateColumns = template.TemplateColumns.ToArray();
                var country = bxProcess.CountryRef;

                //Get the distinct claim orders that will differentiate between multiple claims
                var retreievedClaimOrders = templateColumns
                    .Where(c => c.ClaimOrder != null)
                    .Select(c => c.ClaimOrder)
                    .Distinct();

                var order = mappedRow.Attributes
                    .ForEntity("new_claim")
                    .Where(c=> c.TemplateColumn.ClaimOrder != null)
                    .Select(c=> c.TemplateColumn.ClaimOrder)
                    .Distinct();
                
                //Get all the common fields between all claims
                var claimAttributeValue = mappedRow.Attributes
                    .ForEntity("new_claim")
                    .ForClaimOrder(null);

                mapAttribute.claimStatus = mappedRow.Attributes
                    .ForAttribute("statuscode")
                    .FirstOrDefault().AsOptionSet();

                CheckClaimAttributes(svc, mappedRow);

                foreach (var claimOrder in retreievedClaimOrders)
                {
                    #region Claim
                    //Get the fileds based on the Claim Order
                    var claimValueBasedOnClaimOrder = mappedRow.Attributes
                        .ForEntity("new_claim")
                        .ForClaimOrder(claimOrder);

                    var reserveFieldRowValue = claimValueBasedOnClaimOrder
                        .ForAttribute("new_reserve")
                        .FirstOrDefault().AsDecimal();

                    var peril = claimValueBasedOnClaimOrder
                        .ForAttribute("new_peril1")
                        .FirstOrDefault().AsString();

                    var subPeril = claimValueBasedOnClaimOrder
                        .ForAttribute("new_subperil")
                        .FirstOrDefault().AsString();

                    mapAttribute.excess = claimValueBasedOnClaimOrder
                        .ForAttribute("new_policyexcess")
                        .FirstOrDefault().AsDecimal();

                    //If reserve field is empty we dont create a claim
                    //if (peril != null && peril != "" && subPeril != null && subPeril != "")
                    if (subPeril != null && subPeril != "")
                    {
                        if (reserveFieldRowValue != null && reserveFieldRowValue > 0)
                        {
                            //GetClaimValues(svc, mappedRow, claimOrder);
                            mapAttribute.Reserve = reserveFieldRowValue;
                                                        
                            if(claimValueBasedOnClaimOrder.Where(c => c.AttributeName == "new_claimedamount").Select(c => c.Value).FirstOrDefault() != null)
                            {
                                mapAttribute.ClaimedAmount = claimValueBasedOnClaimOrder
                                    .ForAttribute("new_claimedamount")
                                    .FirstOrDefault()
                                    .AsDecimal();
                            }
                            //Get the broker claim reference
                            var claimRefValue = mappedRow.Attributes
                                .ForEntity("new_claim")
                                .ForAttribute("new_claimreference")
                                .FirstOrDefault().AsString();

                            mapAttribute.claimFolderReferenceValue = mappedRow.Attributes
                                .ForEntity("new_claim")
                                .ForAttribute("new_claimreference")
                                .FirstOrDefault().AsString();
                            
                            mapAttribute.brokerId = bxProcess.BrokerRef.Id;

                            mapAttribute.bxprocessId = bxProcess.Id;

                            mapAttribute.lossTypeDescription = claimValueBasedOnClaimOrder
                                .ForAttribute("new_losstypedescription")
                                .FirstOrDefault().AsString();

                            mapAttribute.BrokerClaimReference = claimAttributeValue
                                .ForAttribute("new_claimreference")
                                .FirstOrDefault().AsString();

                            var riskSubClass = mappedRow.Attributes.ForEntity("new_claim").ForClaimOrder(claimOrder).ForAttribute("new_insuredrisk").FirstOrDefault();
                            
                            //Map the claim fields to be imported
                            Claim claim = new Claim(svc);

                            if (riskSubClass.Value != "Buildings and Contents")
                            {
                                GetClaimValues(svc, mappedRow, claimOrder, riskSubClass.Value);
                                claim.CreateOrUpdateClaim(mapAttribute);
                            }
                            else
                            {
                                GetClaimValues(svc, mappedRow, claimOrder, "Buildings");
                                claim.CreateOrUpdateClaim(mapAttribute);
                                GetClaimValues(svc, mappedRow, claimOrder, "Contents");
                                claim.CreateOrUpdateClaim(mapAttribute);
                                //continue;
                            }
                            
                            //createdClaimId = claim.CreateOrUpdateClaim();

                            //if (createdClaimId != Guid.Empty)
                            //    createdClaim.Add(createdClaimId);
                    #endregion Claim

                   #region Payment
                            //Get the payment fields and map
                            ClaimPayment payment = new ClaimPayment(svc, mappedRow, country, claimOrder);

                            payment.CreateOrUpdatePayment(createdClaimId, mapAttribute.policy.FirstOrDefault());

                            if(mapAttribute.claimStatus.Value == 100000001)
                            {
                                payment.UpdateClaimPaymentStatus(createdClaimId);
                            }

                   #endregion Payment

                   #region Recovery
                            ClaimRecovery recovery = new ClaimRecovery(svc, mappedRow, country, claimOrder);
                            recovery.CreateOrUpdateRecovery(createdClaimId, mapAttribute.policy.FirstOrDefault());

                            if (mapAttribute.claimStatus.Value == 100000001)
                            {
                                recovery.UpdateClaimRecoveryStatus(createdClaimId);
                                //payment.UpdateClaimPaymentStatus(createdClaimId);
                            }

                   #endregion
                        }
                    }
                }

                #region RoleInClaim
                //var retreievedRoleTypeOrder = templateColumns
                //    .Where(c => c.ClaimOrder == claimOrder && c.ClaimRoleTypeOrder != null)
                //    .Select(c => c.ClaimRoleTypeOrder).Distinct();

                var retreievedRoleTypeOrder = templateColumns
                   .Where(c => c.ClaimRoleTypeOrder != null)
                   .Select(c => c.ClaimRoleTypeOrder).Distinct();

                Guid createdVehicleId = Guid.Empty;

                Entity roleInClaim = null;

                foreach (var roleTypeOrder in retreievedRoleTypeOrder)
                {
                    //var allRoleAttributes = mappedRow.Attributes.ForClaimOrder(claimOrder).ForRoleNumber(roleTypeOrder);
                    var allRoleAttributes = mappedRow.Attributes.ForRoleNumber(roleTypeOrder);
                    var contactFirstname = allRoleAttributes.ForAttribute("firstname").FirstOrDefault().AsString();
                    var contactLastName = allRoleAttributes.ForAttribute("lastname").FirstOrDefault().AsString();
                    if (contactFirstname != null && contactLastName != null)
                        roleInClaim = CreateRoleInClaim(svc, allRoleAttributes, createdClaimId, country);

                    var riskClass = bxProcess.RiskClassRef.Name;
                    if (riskClass.ToLower() == "vehicle")
                    {
                        var check = allRoleAttributes.Where(c => c.AttributeName == "new_registrationnumber").FirstOrDefault();

                        if (check != null)
                        {
                        var vehicleRegistrationNo = allRoleAttributes.ForAttribute("new_registrationnumber").FirstOrDefault().AsString();
                        if (vehicleRegistrationNo != null)
                            createdVehicleId = CreateVehicle(svc, allRoleAttributes, createdClaimId);

                        if (createdVehicleId != Guid.Empty)
                        {
                            roleInClaim["new_vehicleid"] = new EntityReference("new_vehicle", createdVehicleId);
                            svc.Update(roleInClaim);
                        }
                    }
                }
                }
        
                #endregion
            }
            catch (InvalidPluginExecutionException e)
            {
                throw new InvalidPluginExecutionException(e.Message, e.InnerException);
            }
            //catch (Exception ex)
            //{
            //    throw new Exception(ex.Message);
            //}
            return createdClaim;
        }
        #endregion MapClaim

        #region CreateRoleType
        private Entity CreateRoleInClaim(IOrganizationService svc, IEnumerable<MappedAttribute> roleTypeFields, Guid createdClaimId, EntityReference defaultCountry)
        {
            try
            {
                Guid createdVehicleId = Guid.Empty;

                var claim = svc.Retrieve("new_claim", createdClaimId, new ColumnSet(true));
                var claimFolderId = claim.GetAttributeValue<EntityReference>("new_claimfolder").Id;

                var partyRef = svc.ProcessParty(roleTypeFields, defaultCountry);
                var roleInClaim = new Entity("new_roleinclaim");
                //roleInClaim["new_claim"] = new EntityReference("new_claim", createdClaimId);
                roleInClaim["new_roletype"] = new EntityReference("new_roletype", roleTypeFields.FirstOrDefault().TemplateColumn.RoleTypeRef.Id);
                roleInClaim["new_claimfolder"] = new EntityReference("new_claimfolder", claimFolderId);

                if (partyRef.LogicalName == "contact")
                    roleInClaim["new_contact"] = partyRef;
                if (partyRef.LogicalName == "account")
                    roleInClaim["new_company"] = partyRef;

                var createdRoleTypeId = svc.Create(roleInClaim);

                return svc.Retrieve("new_roleinclaim", createdRoleTypeId, new ColumnSet(true));

                }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Could not create role", new Exception(ex.Message));
            }
        }
        #endregion

        #region CreateVehicle
        private Guid CreateVehicle(IOrganizationService svc, IEnumerable<MappedAttribute> roleTypeFields, Guid createdRoleTypeId)
        {
            try
            {
                Guid vehicleMakeId = Guid.Empty;
                Guid vehicleModelId = Guid.Empty;
                var vehicleMake = roleTypeFields.ForAttribute("new_manufacturerid").FirstOrDefault().AsString();
                var vehicleModel = roleTypeFields.ForAttribute("new_modelid").FirstOrDefault().AsString();
                if (vehicleMake != null)
                {
                    vehicleMakeId = svc.RetrieveMultipleByName("new_manufacturer", vehicleMake).FirstOrDefault().Id;
                    if (vehicleMakeId == Guid.Empty)
                        vehicleMakeId = CreateVehicleMake(svc, vehicleMake);
                }

                if (vehicleModel != null)
                {
                    vehicleModelId = svc.RetrieveMultipleByName("new_model", vehicleModel).FirstOrDefault().Id;
                    if (vehicleModelId == Guid.Empty)
                        vehicleModelId = CreateVehicleModel(svc, vehicleModel, vehicleMakeId);
                }

                var vehicle = new Entity("new_vehicle");
                vehicle["new_registrationnumber"] = roleTypeFields.ForAttribute("new_registrationnumber").FirstOrDefault().AsString();
                if (vehicleMakeId != Guid.Empty)
                    vehicle["new_manufacturerid"] = new EntityReference("new_manufacturer", vehicleMakeId);
                if (vehicleModelId != Guid.Empty)
                    vehicle["new_modelid"] = new EntityReference("new_model", vehicleModelId);
                
                return svc.Create(vehicle);
            }
            catch (Exception ex)
            {
                return Guid.Empty;
            }
        }

        private Guid CreateVehicleMake(IOrganizationService svc, string vehicleMake)
        {
            try
            {
                Entity make = new Entity("new_manufacturer");
                make["new_name"] = vehicleMake;
                return svc.Create(make);
            }
            catch (Exception ex)
            {
                return Guid.Empty;
            }
        }

        private Guid CreateVehicleModel(IOrganizationService svc, string vehicleModel, Guid vehicleMakeId)
        {
            try
            {
                Entity model = new Entity("new_model");
                model["new_name"] = vehicleModel;
                if (vehicleMakeId != Guid.Empty)
                    model["new_manufacturerid"] = new EntityReference("new_manufacturer", vehicleMakeId);
                return svc.Create(model);
            }
            catch (Exception ex)
            {
                return Guid.Empty;
            }
        }
        #endregion

        #region CheckClaimAttributes
        private void CheckClaimAttributes(IOrganizationService svc, MappedRow mappedRow)
        {
            try
            {
                mapAttribute.claimNotificationDate = mappedRow.Attributes.ForEntity("new_claim").ForAttribute("new_notificationdate").FirstOrDefault();
                
                var lossDate = mappedRow.Attributes.ForEntity("new_claim").ForAttribute("new_dateofloss").FirstOrDefault();

                //Get the loss date
                mapAttribute.lossDateField = lossDate.AsDateTime().Value;
                
                if (mapAttribute.lossDateField > mapAttribute.claimNotificationDate.AsDateTime())
                    throw new InvalidPluginExecutionException(lossDate.TemplateColumn.ColumnLabel, new Exception("Loss date cannot be after the notification date"));

                if (mapAttribute.lossDateField > mapAttribute.claimNotificationDate.AsDateTime())
                    throw new InvalidPluginExecutionException(lossDate.TemplateColumn.ColumnLabel, new Exception("Loss date cannot be after the notification date"));

                if (mapAttribute.claimNotificationDate.AsDateTime() > DateTime.Now)
                    throw new InvalidPluginExecutionException(mapAttribute.claimNotificationDate.TemplateColumn.ColumnLabel, new Exception("Loss date cannot be after the notification date"));

                mapAttribute.policyNumber = mappedRow.Attributes.ForEntity("new_claim").ForAttribute("new_policyversion").FirstOrDefault();
                
                //Check if the Policy exists
                mapAttribute.policy = svc.RetrieveMultipleByName("new_policyfolder", mapAttribute.policyNumber.AsString());
                
                if (mapAttribute.policy.Count() == 0)
                    throw new InvalidPluginExecutionException(mapAttribute.policyNumber.TemplateColumn.ColumnLabel, new Exception("No Policy found with number='{0}'.".FormatWith(mapAttribute.policyNumber.Value)));

                if (mapAttribute.policy.Count() > 1)
                    throw new InvalidPluginExecutionException(mapAttribute.policyNumber.TemplateColumn.ColumnLabel, new Exception("Multiple Policies found with number='{0}'.".FormatWith(mapAttribute.policyNumber.Value)));

                mapAttribute.policyVersion = svc.RetrivePolicyVersionBasedOnPolicy(mapAttribute.policy.FirstOrDefault().Id, mapAttribute.lossDateField);

                if (mapAttribute.policyVersion == null)
                    throw new InvalidPluginExecutionException("Could not find a Policy version", new Exception("Could not find a policy version"));
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(ex.Message, ex.InnerException);
            }
        }
        #endregion

        #region GetClaimValues
        private void GetClaimValues(IOrganizationService svc, MappedRow mappedRow, int? claimOrder, string riskSubClass)
        {
            ThrowIf.Argument.IsNull(riskSubClass, "RiskSubClass");
            try
            {
                var productId = mapAttribute.policy.First().GetAttributeValue<EntityReference>("new_product").Id;

                //var riskSubClass = mappedRow.Template.TemplateColumns.Where(c => c.AttributeName == "new_insuredrisk").Select(c => c.RiskSubClass).FirstOrDefault();

                //var insuredRiskDefault = mappedRow.Template.TemplateColumns.Where(c => c.AttributeName == "new_insuredrisk").Select(c => c.DefaultValue).FirstOrDefault();

                if (productId == Guid.Empty)
                    throw new InvalidPluginExecutionException("", new Exception("Product not found for policy '{0}'.".FormatWith(mapAttribute.policy.FirstOrDefault().GetAttributeValue<string>("new_name"))));
                
                //var insuredRiskFromBordereau = mappedRow.Attributes.ForEntity("new_claim").ForClaimOrder(claimOrder).ForAttribute("new_insuredrisk").FirstOrDefault();

                //if (insuredRiskFromBordereau.Value != null)
                //    mapAttribute.insuredRisk = svc.RetrieveMultipleByName("new_insuredrisk", insuredRiskFromBordereau.AsString());
                ////    mapAttribute.insuredRisk = svc.RetrieveInsuredRiskForClaim("new_insuredrisk", mapAttribute.policy.FirstOrDefault().Id);
                //else if (insuredRiskDefault != null)
                //    mapAttribute.insuredRisk = svc.RetrieveInsuredRisk("new_insuredrisk", insuredRiskDefault, riskSubClass.Id);
                //else
                //    mapAttribute.insuredRisk = svc.RetrieveInsuredRiskForClaim("new_insuredrisk", mapAttribute.policy.FirstOrDefault().Id);

                //var riskSubClass = mappedRow.Attributes.ForEntity("new_claim").ForClaimOrder(claimOrder).ForAttribute("new_insuredrisk").FirstOrDefault();

                var retrievedRiskSubClass = svc.RetriveRiskSubClassByName("new_riskclass", riskSubClass);

                mapAttribute.insuredRisk = svc.RetriveInsuredRisk(mapAttribute.policyVersion.Id, productId, retrievedRiskSubClass.Id);

                if (mapAttribute.insuredRisk.Count() > 1)
                    throw new InvalidPluginExecutionException("", new Exception("Multiple Insured risks found, please supply an Insured risk"));

                if (mapAttribute.insuredRisk.Count() == 0)
                    throw new InvalidPluginExecutionException("", new Exception("No Insured risk found"));

                var riskClass = mapAttribute.insuredRisk.FirstOrDefault().GetAttributeValue<EntityReference>("new_riskclassid");

                var productName = mapAttribute.policy.First().GetAttributeValue<EntityReference>("new_product").Name;

                //var lossTypeField = mappedRow.Attributes.ForEntity("new_claim").ForClaimOrder(claimOrder).ForAttribute("new_peril1").FirstOrDefault();

                var subPeril = mappedRow.Attributes.ForEntity("new_claim").ForClaimOrder(claimOrder).ForAttribute("new_subperil").FirstOrDefault();

                //Retrieve the loss type from CRM
                //mapAttribute.lossType = svc.RetrieveLossType("new_losstype", lossTypeField.AsString());

                mapAttribute.subPeril = svc.RetrieveSubPeril(subPeril.Value).FirstOrDefault(); //subPeril.AsEntity();

                //If loss type not found in CRM throw exception and stop processing
                //if (mapAttribute.lossType.Count == 0)
                //    throw new InvalidPluginExecutionException(lossTypeField.TemplateColumn.ColumnLabel, new Exception("No Peril found for '{0}'.".FormatWith(lossTypeField.Value)));

                if (mapAttribute.subPeril == null)
                    throw new InvalidPluginExecutionException(subPeril.TemplateColumn.ColumnLabel, new Exception("No Sub Peril found for '{0}'.".FormatWith(subPeril.Value)));

                #region commentedcode
                //var coveredPeril = svc.RetrieveCoveredPeril("new_coveredperil", riskClass.Id, mapAttribute.subPeril.Id).FirstOrDefault();

                ////Get the asscoiated loss type code
                //var lossTypeCode = mapAttribute.lossType.Select(c => c.Attributes["new_losstypecode"]).FirstOrDefault();

                ////Get the basic cover id which is a lookup on the Loss type record
                //Guid basicCoverId = coveredPeril.GetAttributeValue<EntityReference>("new_basiccover").Id;
                ////Guid basicCoverId = mapAttribute.lossType.FirstOrDefault().GetAttributeValue<EntityReference>("new_basiccoverid").Id;

                //var basicCoverName = coveredPeril.GetAttributeValue<EntityReference>("new_basiccover").Name;
                ////var basicCoverName = mapAttribute.lossType.FirstOrDefault().GetAttributeValue<EntityReference>("new_basiccoverid").Name;

                ////If Basic cover not found in CRM throw exception 
                //if (basicCoverId == Guid.Empty)
                //    throw new InvalidPluginExecutionException(lossTypeField.TemplateColumn.ColumnLabel, new Exception("No Basic Cover associated with loss type '{0}'.".FormatWith(lossTypeField.Value)));

                ////Get the Cover based on the Basic Cover Id
                //var retrievedCover = svc.RetrieveCover("new_cover", basicCoverId, productId);

                ////If loss type not found in CRM throw exception 
                //if (retrievedCover.Count == 0)
                //    throw new InvalidPluginExecutionException(lossTypeField.TemplateColumn.ColumnLabel, new Exception("No Cover found with basic cover '{0}' and Product '{1}'.".FormatWith(basicCoverName, productName)));

                //mapAttribute.retrievedCoverId = Guid.Empty;

                //var coverName = retrievedCover.Select(c => c.GetAttributeValue<string>("new_name")).FirstOrDefault();

                ////We should get an unique cover based on the Basic Cover id and Product Id
                //if (retrievedCover.Count > 1)
                //    throw new InvalidPluginExecutionException(lossTypeField.TemplateColumn.ColumnLabel, new Exception("Multiple Covers found with basic cover '{0}' and Product '{1}'.".FormatWith(basicCoverName, productName)));
                //else
                //    mapAttribute.retrievedCoverId = retrievedCover.Select(c => c.Id).FirstOrDefault();

                ////Retrieve a list of policy version based on Policy no 
                //mapAttribute.policyVersion = svc.RetrievePolicyVersion("new_policy", mapAttribute.policy.First().Id, mapAttribute.lossDateField, mapAttribute.retrievedCoverId);

                //if (mapAttribute.policyVersion.Count == 0)
                //    throw new InvalidPluginExecutionException(mapAttribute.policyNumber.TemplateColumn.ColumnLabel, new Exception("No Policy Version found with Policy number '{0}' and Loss date '{1}' and Cover '{2}'.".FormatWith(mapAttribute.policyNumber.Value, mapAttribute.lossDateField, coverName)));

                //Guid policyVersionId = mapAttribute.policyVersion.Select(c => c.Id).FirstOrDefault();

                //var policyVersionNumber = mapAttribute.policyVersion.Select(c => c.GetAttributeValue<string>("new_name")).FirstOrDefault();

                ////Retrieve the Insured cover based on Policy Id and cover ID
                //mapAttribute.insuredCover = svc.RetrieveInsuredCover("new_insuredcover", policyVersionId, mapAttribute.retrievedCoverId);
                
                //if (mapAttribute.insuredCover.Count == 0)
                //    throw new InvalidPluginExecutionException(mapAttribute.policyNumber.TemplateColumn.ColumnLabel, new Exception("No insured cover found for Policy version number '{0}' and cover '{1}'.".FormatWith(policyVersionNumber, coverName)));

                //mapAttribute.insuredRisk = svc.RetrieveRisk("new_insuredrisk", policyVersionId, productId);

                //if (mapAttribute.insuredRisk.Count > 1)
                //{
                //    var insuredRiskFromExcel = mappedRow.Attributes.ForEntity("new_claim").ForAttribute("new_insuredrisk").FirstOrDefault();
                //    throw new InvalidPluginExecutionException(mapAttribute.policyNumber.TemplateColumn.ColumnLabel, new Exception("Multiple insured risk found for Policy version number '{0}' and cover '{1}'.".FormatWith(policyVersionNumber, coverName)));
                //}
                #endregion
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(ex.Message, ex.InnerException);
            }
        }
        #endregion
        
    }
}
