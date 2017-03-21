using Elite.CRM.Plugins.BordereauImport;
using Elite.CRM.Plugins.Entities;
using Elite.CRM.Plugins.ErrorHandling;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Xrm.Sdk.Query;
using System.IO;
using Elite.CRM.Plugins.SharePoint;
using System.Net;
using Microsoft.Crm.Sdk.Messages;
using System.Activities;
using Microsoft.Xrm.Sdk.Messages;
using System.Net.Mail;

namespace Elite.CRM.Plugins
{
    public class BordereauProcessMapper : BasePlugin
    {
        private Dictionary<string, string> _config;
        private bool _trace = false;

        public enum BordereauType
        {
            Policy = 100000000,
            Claim = 100000001,
            Payment = 100000002
        }

        public BordereauProcessMapper(string unsecureConfig, string secureConfig)
            : base(unsecureConfig, secureConfig)
        {
            RegisterEvent(PluginStage.PostOperation, PluginMessage.Update, "new_bordereauxprocess", MapBordereauRows);
            RegisterEvent(PluginStage.PostOperation, PluginMessage.Update, "new_bordereauximportrow", MapImportRow);
            RegisterEvent(PluginStage.PostOperation, PluginMessage.Create, "new_bordereauxprocess", CreateUpdateMonthlyBordereau);

            _config = ParseConfig(unsecureConfig);

            if (_config != null)
            {
                if (_config.ContainsKey("trace") && "true".Equals(_config["trace"], StringComparison.InvariantCultureIgnoreCase))
                    _trace = true;
            }
        }

        #region Bordereau process and row plugins

        protected void MapBordereauRows(LocalPluginContext context)
        {
            var preBordereauProcess = new BordereauProcess(context.OrganizationService, context.TracingService, context.PreImage);
            var bordereauProcess = new BordereauProcess(context.OrganizationService, context.TracingService, context.PostImage);

            if (!CheckProcessStatusTransition(preBordereauProcess, bordereauProcess))
                return;

            if (!_config.ContainsKey("SharePointUrl"))
                throw new Exception("Plug-in step configuration does not contain value for 'SharePointUrl'. Please add SharePointUrl='http://[your SharePoint URL]' to unsecure configuration of this plug-in step.");

            context.Trace("Retrieving import rows.");
            var newImportRows = bordereauProcess.RetrieveImportRowRefs(BordereauImportRowStatus.New);
            var template = bordereauProcess.BordereauTemplate;
            var rows = bordereauProcess.ImportRows;
            List<MappedRow> mappedRowList = new List<MappedRow>();
            foreach (var row in rows)
            {
                var mappedRow = new MappedRow(row, template);
                mappedRowList.Add(mappedRow);
            }

            var grossPremium = mappedRowList.SelectMany((p => p.Attributes.ForEntity("new_policy").ForAttribute("new_grosspremium")));

            var sumOfGrossPremium = grossPremium.Sum(p => decimal.Parse(p.Value));

            var entity = new Entity(context.PostImage.LogicalName);
            entity.Id = context.PostImage.Id;
            entity["new_grosspremiumbordereau"] = sumOfGrossPremium;

            context.OrganizationService.Update(entity);

            context.Trace("Processing {0} rows for current bordereau.", newImportRows.Count());

            var multiRequestSettings = new ExecuteMultipleSettings()
            {
                ContinueOnError = true,
                ReturnResponses = false
            };

            foreach (var row in newImportRows)
            {
                var multiRequest = new ExecuteMultipleRequest() { Settings = multiRequestSettings };
                var stateRequest = new SetStateRequest()
                {
                    EntityMoniker = row,
                    State = CustomEntityStatus.Active.ToOptionSet(),
                    Status = BordereauImportRowStatus.Loading.ToOptionSet()
                };

                multiRequest.Requests = new OrganizationRequestCollection() { stateRequest };

                var multiResponse = context.OrganizationService.Execute(multiRequest) as ExecuteMultipleResponse;

                if (multiResponse.IsFaulted)
                {
                    var fault = multiResponse.Responses.First().Fault;

                    // code for SQL Timeout - possible hang/deadlock 
                    if (fault.ErrorCode == -2147204783)
                        throw new FaultException<OrganizationServiceFault>(fault);

                    // otherwise, set state to failed
                    var failedStateRequest = new SetStateRequest()
                    {
                        EntityMoniker = row,
                        State = CustomEntityStatus.Active.ToOptionSet(),
                        Status = BordereauImportRowStatus.Failed.ToOptionSet()
                    };

                    context.OrganizationService.Execute(failedStateRequest);

                    if (_trace)
                    {
                        var traceRow = new Entity("new_bordereauximportrow") { Id = row.Id };
                        traceRow["new_trace"] = fault.TraceText;
                        context.OrganizationService.Update(traceRow);
                    }

                    // TODO create Bordereau error for unexpected exception

                }
            }

            // TODO send errors to SharePoint

            // set status of process
            var processStateRequest = new SetStateRequest()
            {
                EntityMoniker = bordereauProcess.EntityReference,
                State = CustomEntityStatus.Active.ToOptionSet(),
                Status = BordereauProcessStatus.Completed.ToOptionSet()
            };

            context.OrganizationService.Execute(processStateRequest);

            if (newImportRows != null)
                SendEmailToUnderWriter(bordereauProcess);
        }

        protected void MapImportRow(LocalPluginContext context)
        {
            var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;

            if (!CheckRowStatusTransition(context.PreImage, target))
                return;

            var rowEntity = context.OrganizationService.RetrieveNoLock(target.LogicalName, "new_bordereauximportrowid", target.Id);
            var row = new BordereauImportRow(context.OrganizationService, context.TracingService, rowEntity);

            var bordereauProcess = row.BordereauProcess;

            context.Trace("Retrieving bordereau template.");
            var template = bordereauProcess.BordereauTemplate;
            if (template == null)
                throw new InvalidPluginExecutionException("Unable to find a template for broker '{0}' and risk class '{1}'.".FormatWith(bordereauProcess.BrokerRef.Name, bordereauProcess.RiskClassRef.Name));

            context.Trace("Checking bordereau type.");
            var bxType = bordereauProcess.BordereauType;
            if (!bxType.HasValue)
                throw new InvalidPluginExecutionException("no BX type");

            var mappedRow = new MappedRow(row, template);

            try
            {
                var errors = Enumerable.Empty<BordereauError>();

                switch (bxType.Value)
                {
                    case BordereauProcessBordereauType.Claim:
                        context.Trace("Importing claim row.");
                        errors = ImportClaim(context, mappedRow, template, bordereauProcess);
                        break;
                    case BordereauProcessBordereauType.Policy:
                        context.Trace("Importing policy row.");
                        errors = ImportPolicy(context, mappedRow, template, bordereauProcess);
                        break;
                }

                if (!errors.Any())
                {
                    // no errors, set status to completed
                    var entity = new Entity(target.LogicalName);
                    entity.Id = target.Id;
                    entity["new_importstatus"] = BordereauImportRowStatus.Completed.ToOptionSet();
                    context.OrganizationService.Update(entity);

                    //var resultStateRequest = new SetStateRequest()
                    //{
                    //    EntityMoniker = target.ToEntityReference(),
                    //    State = CustomEntityStatus.Active.ToOptionSet(),
                    //    Status = BordereauImportRowStatus.Completed.ToOptionSet()
                    //};

                    context.Trace("Setting status to Completed");
                    //context.OrganizationService.Execute(resultStateRequest);
                    context.Trace("Completed setting status to Completed");
                }
                else
                {
                    // log errors into CRM
                    foreach (var bxError in errors)
                    {
                        bordereauProcess.CreateErrorRecord(bxError);
                    }

                    var entity = new Entity(target.LogicalName);
                    entity.Id = target.Id;
                    entity["new_importstatus"] = BordereauImportRowStatus.Failed.ToOptionSet();
                    context.OrganizationService.Update(entity);

                    // set status to failed
                    //var resultStateRequest = new SetStateRequest()
                    //{
                    //    EntityMoniker = target.ToEntityReference(),
                    //    State = CustomEntityStatus.Active.ToOptionSet(),
                    //    Status = BordereauImportRowStatus.Failed.ToOptionSet()
                    //};

                    context.Trace("Setting status to failed");
                    //context.OrganizationService.Execute(resultStateRequest);
                    context.Trace("Completed Setting status to failed");
                }

            }
            catch (BordereauException ex)
            {
                context.Trace("Bordereau Exception: {0}", ex.Message);

                var error = ex.Error;

                if (error == null)
                {
                    error = new BordereauError(BordereauErrorType.BusinessError, ex.Message);
                    error.RowNumber = row.RowNumber.Value;
                }

                error.RowNumber = row.RowNumber.Value;

                bordereauProcess.CreateErrorRecord(error);

                var entity = new Entity(target.LogicalName);
                entity.Id = target.Id;
                if (error.Message != "Duplicate Policy")
                    entity["new_importstatus"] = BordereauImportRowStatus.Failed.ToOptionSet();
                else
                    entity["new_importstatus"] = BordereauImportRowStatus.Duplicate.ToOptionSet();
                context.OrganizationService.Update(entity);

                // set status to failed
                //var resultStateRequest = new SetStateRequest()
                //{
                //    EntityMoniker = target.ToEntityReference(),
                //    State = CustomEntityStatus.Active.ToOptionSet(),
                //    Status = BordereauImportRowStatus.Failed.ToOptionSet()
                //};

                //context.OrganizationService.Execute(resultStateRequest);
            }
            catch (Exception ex)
            {
                var error = new BordereauError(BordereauErrorType.BusinessError, ex.Message);
                error.RowNumber = row.RowNumber.Value;

                bordereauProcess.CreateErrorRecord(error);

                var exceptionInfo = BasePlugin.DumpException(ex);

                context.Trace(exceptionInfo);

                var entity = new Entity(target.LogicalName);
                entity.Id = target.Id;
                entity["new_importstatus"] = BordereauImportRowStatus.Failed.ToOptionSet();
                context.OrganizationService.Update(entity);

                //var resultStateRequest = new SetStateRequest()
                //{
                //    EntityMoniker = target.ToEntityReference(),
                //    State = CustomEntityStatus.Active.ToOptionSet(),
                //    Status = BordereauImportRowStatus.Failed.ToOptionSet()
                //};

                //context.OrganizationService.Execute(resultStateRequest);
                if (_trace)
                {

                    var traceRow = new Entity("new_bordereauximportrow") { Id = target.Id };
                    traceRow["new_trace"] = context.GetTraceContent();
                    context.OrganizationService.Update(traceRow);
                }

                throw;
            }

            if (_trace)
            {

                var traceRow = new Entity("new_bordereauximportrow") { Id = target.Id };
                traceRow["new_trace"] = context.GetTraceContent();
                context.OrganizationService.Update(traceRow);
            }
        }

        #endregion

        /// <summary>
        /// Creates policy based on Import row values. If validation of a row fails, returns a list of validation errors.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="mappedRow"></param>
        /// <param name="bxTemplate"></param>
        /// <param name="bxProcess"></param>
        /// <returns></returns>
        private IEnumerable<BordereauError> ImportPolicy(LocalPluginContext context, MappedRow mappedRow, BordereauTemplate bxTemplate, BordereauProcess bxProcess)
        {
            ThrowIf.Argument.IsNull(context, "context");
            ThrowIf.Argument.IsNull(mappedRow, "mappedRow");
            ThrowIf.Argument.IsNull(bxTemplate, "bxTemplate");
            ThrowIf.Argument.IsNull(bxProcess, "bxProcess");
            //try
            //{

            var defaults = new PolicyMapperDefaults()
            {
                Country = bxProcess.CountryRef,
                Currency = bxProcess.CurrencyRef,
                Product = bxProcess.ProductRef,
                BordereauProcess = bxProcess.EntityReference,
                Broker = bxProcess.BrokerRef,
                MonthlyBordereau = bxProcess.MonthlyBordereau,
            };

            var policyMapper = new PolicyMapper(context.OrganizationService, mappedRow, defaults);
            policyMapper.SetTracingService(context.TracingService);

            // validate row and push errors 
            context.Trace("Validating row.");
            var errors = policyMapper.Validate();

            #region trace errors & continue to next row

            if (errors.Any())
            {
                TraceCurrentRowErrors(context, policyMapper.PolicyNumber, mappedRow.RowNumber.Value, errors);
                context.Trace("Skipping row {0}.".FormatWith(mappedRow.RowNumber));
                return errors;
            }

            #endregion

            context.Trace("Row {0} valid, starting creation of records.", mappedRow.RowNumber);

            EntityReference policyHolder = null;
            try
            {
                policyHolder = policyMapper.ProcessPolicyHolder();
                if (policyHolder == null)
                {
                    throw BordereauException.DataError("Unable to create policyholder.");
                }
            }
            catch (Exception ex)
            {
                //context.Trace(ex.Message, ex);
                throw new Exception("Could not Create Policyholder. Exception message is : " + ex.Message);
            }

            context.Trace("Policyholder processed.");

            var product = policyMapper.ResolveProduct();
            context.Trace("Product resolved: {0}", product.Entity.GetAttributeValue<string>("new_name"));

            //var riskClass = bxTemplate.RiskClass;
            //context.Trace("Risk class: {0}", riskClass.Entity.GetAttributeValue<string>("new_name"));

            context.Trace("Processing policy version...");
            var policyVersion = policyMapper.CreatePolicyVersion(policyHolder, product.EntityReference);


            // null policy version means re-upload of same data without changes
            if (policyVersion == null)
            {
                var error = new BordereauError(BordereauErrorType.BusinessError, "Duplicate Policy");
                throw BordereauException.DataError("Duplicate Policy", error);
            }
            //return Enumerable.Empty<BordereauError>();

            // TODO refactor insured risk creation from below into this empty method
            policyMapper.CreateOptionalInsuredRisks();

            //Create Insured risk for products where risks could be 0 or >1
            var risks = mappedRow.Attributes.ForEntity("new_insuredrisk").ForAttribute("new_riskid");

            //if (risks.Count() == 0)
            //    throw BordereauException.DataError("Atleast one Insured risk is needed ");

            //var checkRisks = mappedRow.Attributes
            //    .ForEntity("new_insuredrisk")
            //    .ForAttribute("new_riskid")
            //    .Where(c => c.Value != null)
            //    .FirstOrDefault();

            //if(checkRisks == null)
            //{
            //    throw BordereauException.DataError("Atleast one Insured risk is needed ");
            //}

            var createdInsuredRisks = CreateInsuredRisks(context, policyVersion, risks, policyMapper);

            policyMapper.UpdateInsuredRisks(policyVersion);

            IEnumerable<MappedAttribute> coverFromBordereau = null;
            //Create Accidental damage Insured cover if it has a value in the Excel
            if(mappedRow.Attributes.ForEntity("new_insuredcover") != null)
                coverFromBordereau = mappedRow.Attributes.ForEntity("new_insuredcover").ForAttribute("new_coverid");

            foreach(var cover in coverFromBordereau)
            {
                var riskSubClass = cover.TemplateColumn.RiskSubClass;
                if (riskSubClass != null)
                {
                    var perilId = cover.TemplateColumn.PerilId;
                    var perilRef = cover.TemplateColumn.Peril;
                    if(perilRef == null)
                        throw new Exception("Could not Create Insured Peril. Exception message is : " + "Peril column on Bordereau is null");

                    var risk = context.OrganizationService.RetriveRiskBasedOnSubClass(riskSubClass.Id, product.Id).FirstOrDefault();
                    var perilSection = context.OrganizationService.RetrievePerilSection(perilRef.Id, risk.Id, product.Id);   //.Retrieve(perilSectionRef.LogicalName, perilSectionRef.Id, new ColumnSet(true)); 
                    //var riskId = perilSection.GetAttributeValue<EntityReference>("new_riskid").Id;
                    var insuredRisk = context.OrganizationService.RetrieveInsuredRiskForPolicy(policyVersion.Id, risk.Id).FirstOrDefault();
                    
                    if(perilId == null)
                    {
                        if(cover != null && cover.Value != null)
                        {
                            if (insuredRisk != null)
                                CreateInsuredCoversForInsuredRisk(context, insuredRisk, perilSection);
                        }
                    }
                    else if(cover.Value == perilId)
                    {
                        CreateInsuredCoversForInsuredRisk(context, insuredRisk, perilSection);
                    }
                    else
                    {
                        throw new Exception("Could not Create Insured Cover. Exception message is : " + "Peril data does not match on the template and excel");
                        //throw BordereauException.DataError("Peril data does not match on the template and excel");
                    }
                }


            }
          
            #region commentedcode
            //if (coverFromBordereau != null)
            //{
            //    if (coverFromBordereau.Value != null && coverFromBordereau.Value.ToLowerInvariant() != "no")
            //    {
            //        var perilRef = coverFromBordereau.TemplateColumn.Peril;
                    
            //        var perilSection = context.OrganizationService.RetrievePerilSection(perilRef.Id, riskSubClass.Id, product.Id);   //.Retrieve(perilSectionRef.LogicalName, perilSectionRef.Id, new ColumnSet(true)); 
            //        var riskId = perilSection.GetAttributeValue<EntityReference>("new_riskid").Id;
            //        var insuredRisk = context.OrganizationService.RetrieveInsuredRiskForPolicy(policyVersion.Id, riskId).FirstOrDefault();

            //        if (insuredRisk != null)
            //            CreateInsuredCoversForInsuredRisk(context, insuredRisk, perilSection);
            //    }
            //}
            #endregion

            //We need to review this part of the code.
            //This has been commented for now so Krishna can continue with his development work

            //foreach (var insuredRisk in createdInsuredRisks)
            //{
            //    var riskClassRef = insuredRisk.GetAttributeValue<EntityReference>("new_riskclassid");
            //    var riskClassEntity = context.OrganizationService.Retrieve(riskClassRef);
            //    var riskClass = new RiskClass(context.OrganizationService, context.TracingService, riskClassEntity);
            //    var riskEntity = policyMapper.ImportRiskEntity(riskClass);
            //    policyMapper.AddRiskToPolicy(policyVersion, riskEntity, riskClass.InsuredRiskLookup);
            //}

            #region Roles

            var policyVersionWrapped = new PolicyVersion(context.OrganizationService, context.TracingService, policyVersion);
            var policyFolderRef = policyVersionWrapped.PolicyFolder.EntityReference;

            foreach (var roleType in bxTemplate.UniqueRoleTypeIDs)
            {
                policyMapper.AddRoleToPolicy(roleType, policyFolderRef);
            }

            #endregion

            // trigger recalculation of totals on policy version
            var triggerPolicy = new Entity(policyVersion.LogicalName) { Id = policyVersion.Id };
            triggerPolicy["new_recalc"] = true;
            context.OrganizationService.Update(triggerPolicy);

            
            //}
            //catch(Exception ex)
            //{
            // throw new Exception(ex.Message);

            //}

            // return no errors
            return Enumerable.Empty<BordereauError>();
            
}


        private IEnumerable<BordereauError> ImportClaim(LocalPluginContext context, MappedRow mappedRow, BordereauTemplate bxTemplate, BordereauProcess bxProcess)
        {
            ThrowIf.Argument.IsNull(context, "context");
            //ThrowIf.Argument.IsNull(mappedRow, "mappedRow");
            ThrowIf.Argument.IsNull(bxTemplate, "bxTemplate");
            ThrowIf.Argument.IsNull(bxProcess, "bxProcess");

            var templateColumns = bxTemplate.TemplateColumns;

            var entityName = templateColumns
                .Select(c => c.EntityName.ToString())
                .Distinct().ToList();

            var claimRefValue = mappedRow.Attributes
                    .ForEntity("new_claim")
                    .ForAttribute("new_claimreference")
                    .FirstOrDefault().AsString();

            var errors = new List<BordereauError>();

            #region validation

            errors.AddRange(mappedRow.Attributes.ForEntity("new_claim").Validate());
            errors.AddRange(mappedRow.Attributes.ForEntity("new_payment").Validate());
            errors.AddRange(mappedRow.Attributes.ForEntity("new_claimrecovery").Validate());

            #endregion

            #region trace errors & continue

            if (errors.Any())
            {
                foreach (var error in errors)
                {
                    context.Trace("Error: row {0}, column {1}, error {2} for value '{3}'.".FormatWith(mappedRow.RowNumber, error.Column.ColumnLabel, error.ErrorType, error.Value));
                }

                context.Trace("Skipping row {0}".FormatWith(mappedRow.RowNumber));
                foreach (var err in errors)
                {
                    // TODO set claim identifier
                    //bxProcess.CreateErrorRecord(err);
                    err.RecordIdentifier = claimRefValue;
                    bxProcess.CreateErrorRecords(err, mappedRow);
                }

                return errors;
            }

            #endregion

            #region Create Claim
            try
            {
                var lossTypeField = mappedRow.Attributes
                    .ForEntity("new_claim")
                    .ForAttribute("new_subperil")
                    .FirstOrDefault().AsString();

                var dateOfLoss = mappedRow.Attributes
                    .ForEntity("new_claim")
                    .ForAttribute("new_dateofloss")
                    .FirstOrDefault().AsDateTime();

                var lossType = context.OrganizationService.RetrieveLossType("new_losstype", lossTypeField);

                var retrievedClaim = context.OrganizationService.RetrieveClaim("new_claim", claimRefValue, bxProcess.BrokerRef.Id, lossType.FirstOrDefault().Id, dateOfLoss);

                if (retrievedClaim.Count >= 1)
                {
                    throw new InvalidPluginExecutionException("Bordereau Plugin ", new Exception("duplicate claim found"));
                }
                else
                {
                    MappedClaim mapClaim = new MappedClaim();

                    var Claim = mapClaim.MapClaim(context.OrganizationService, mappedRow, bxTemplate, bxProcess);
                }
            }
            catch (InvalidPluginExecutionException ex)
            {
                if (ex.InnerException != null)
                    errors.Add(new BordereauError(BordereauErrorType.BusinessError, ex.InnerException.Message, ex.Message));
                else
                    errors.Add(new BordereauError(BordereauErrorType.BusinessError, ex.Message));
            }

            #endregion
            #region Record Errors
            foreach (var err in errors)
            {
                err.RecordIdentifier = claimRefValue;
                bxProcess.CreateErrorRecords(err, mappedRow);

                //var errorRecord = new Entity("new_failedrow");  //bxProcess.CreateErrorRecord(err);

                //errorRecord["new_row"] = err.Message;
                //errorRecord["new_errorcolumnlabel"] = err.ColumnLabel;
                //errorRecord["new_errorrow"] = mappedRow.RowNumber;//err.RowNumber;
                //errorRecord["new_name"] = err.ToString();
                //errorRecord["new_bordereauxprocessid"] = new EntityReference(bxProcess.LogicalName, bxProcess.Id); 
                //errorRecord["new_riskclassid"] = new EntityReference(bxProcess.RiskClassRef.LogicalName, bxProcess.RiskClassRef.Id);
                //errorRecord["new_policynumber"] = claimRefValue;

                //context.OrganizationService.Create(errorRecord);
            }

            Entity bordereauProcess = new Entity(bxProcess.LogicalName);
            bordereauProcess.Id = bxProcess.Id;
            //bordereauProcess["new_processed"] = allErrors.RowNumber;
            bordereauProcess["new_errors"] = errors.Count;
            bordereauProcess["new_bordereauxtemplate"] = bxTemplate.EntityReference;
            context.OrganizationService.Update(bordereauProcess);

            int status = 100000001; // Status is Completed
            SetStateRequest state = new SetStateRequest();
            state.State = new OptionSetValue(0);
            state.Status = new OptionSetValue(status); //Change the Status of Bordereaux process from Load in Progress to Ready to import
            state.EntityMoniker = new EntityReference(bxProcess.LogicalName, bxProcess.Id);

            //Execute the Request
            context.OrganizationService.Execute(state);

            //MemoryStream memStream = null;
            //using (memStream = new MemoryStream())
            //{
            //    ErrorFileWriter.WriteErrors(errors, memStream);
            //}
            //var spCreds = new NetworkCredential("siyer", "V01y3_r", "elitecloud");//CredentialCache.DefaultNetworkCredentials;
            //string fileUrl = null;
            //using (var spClient = new SharePointClient("https://sp.elite-insurance.co.uk:7700/", spCreds))         //(_config["SharePointUrl"], spCreds))
            //{
            //    // URL of SP site: https://sp.elite-insurance.co.uk:7700/
            //    // Library:        account
            //    // Folder:         [Broker new_name]/new_bordereauxprocess/[bxProcess new_name]
            //    // FileName:       [bxProcess new_name]E.csv

            //    var folderPath = "{0}/new_bordereauxprocess/{1}".FormatWith(bxProcess.Broker.GetAttributeValue<string>("new_name"), bxProcess.Name);
            //    var fileName = "{0}E.csv".FormatWith(bxProcess.Name);

            //    fileUrl = spClient.UploadFile("account", folderPath, fileName, memStream.ToArray());
            //}

            //var updatedProcess = new Entity(bxProcess.LogicalName);
            //updatedProcess.Id = bxProcess.Id;
            //updatedProcess["new_errorsfile"] = fileUrl;
            //context.OrganizationService.Update(updatedProcess);
            #endregion

            // return no errors
            return Enumerable.Empty<BordereauError>();
        }

        private void CreateUpdateMonthlyBordereau(LocalPluginContext context)
        {
            Entity bordereauProcess = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            BordereauProcess process = new BordereauProcess(context.OrganizationService, context.TracingService, bordereauProcess);
            //var bxCode = bordereauProcess.GetAttributeValue<string>("new_bordereauxcode");
            //var bxMonth = proce //bordereauProcess.GetAttributeValue<OptionSetValue>("new_bordereauxmonth").Value;
            //var bxYear = bordereauProcess.GetAttributeValue<string>("new_bordereauxyear2");
            //var bxBroker = bordereauProcess.GetAttributeValue<string>("new_brokerid");
            //var bxType = bordereauProcess.GetAttributeValue<OptionSetValue>("new_bordereauxtype").Value;
            var monthlyBrodereau = context.OrganizationService.RetrieveMonthlyBordereau(process);

            if (monthlyBrodereau == null)
            {
                monthlyBrodereau = CreateMonthlyBordereau(context, process);

            }

            if (monthlyBrodereau != null)
            {
                Entity bxProcess = new Entity(bordereauProcess.LogicalName);
                bxProcess.Id = bordereauProcess.Id;
                bxProcess["new_monthlybordereau"] = monthlyBrodereau.ToEntityReference();

                context.OrganizationService.Update(bxProcess);
            }

            //var mappedRow = process.BordereauTemplate.TemplateColumns;
        }

        private Entity CreateMonthlyBordereau(LocalPluginContext context, BordereauProcess process)
        {
            var bxProcessName = process.Name.Substring(0, process.Name.IndexOf('V'));
            Entity monthlyBx = new Entity("new_monthlybordereau");
            monthlyBx["new_name"] = bxProcessName;
            monthlyBx["new_month"] = process.BordereauxMonth; //process.BordereauxMonth.Value;
            monthlyBx["new_year"] = process.BordereauxYear;
            monthlyBx["new_broker"] = process.BrokerRef;
            monthlyBx["new_bordereautype"] = process.BordereauType.Value.ToOptionSet();

            var createdMonthlyBx = context.OrganizationService.Create(monthlyBx);

            return context.OrganizationService.Retrieve("new_monthlybordereau", createdMonthlyBx, new ColumnSet(true));
        }

        //private void CreatePolicies(LocalPluginContext context, BordereauTemplate template, BordereauProcess bxProcess)
        //{
        //    ThrowIf.Argument.IsNull(context, "context");
        //    ThrowIf.Argument.IsNull(template, "template");
        //    ThrowIf.Argument.IsNull(bxProcess, "bxProcess");

        //    var importRows = bxProcess.ImportRows;

        //    // Bordereau process with zero rows is ignored
        //    if (!importRows.Any())
        //    {
        //        context.Trace("No rows to process, exiting.");
        //        return;
        //    }

        //    //// transaction name is not mapped
        //    //if (template.GetColumn("new_policy", "new_transactiontype") == null)
        //    //    throw new InvalidPluginExecutionException("Transaction type for policy is not mapped. Please map field 'new_policy.new_transactiontype' to an import column.");

        //    var defaults = new PolicyMapperDefaults()
        //    {
        //        Country = bxProcess.CountryRef,
        //        Currency = bxProcess.CurrencyRef,
        //        Product = bxProcess.ProductRef,
        //        BordereauProcess = bxProcess.EntityReference,
        //        Broker = bxProcess.BrokerRef,
        //    };

        //    // collection of errors in all rows
        //    var allErrors = new ErrorCollection(bxProcess.BordereauTemplate.StartingRow - 1);

        //    foreach (var row in importRows)
        //    {
        //        // move to next row 
        //        allErrors.NextRow();
        //        context.Trace("Processing row {0}.", allErrors.RowNumber);

        //        // create a mapper, which will take care of all data transformations and matching to existing CRM
        //        var mappedRow = new MappedRow(row, template);
        //        var policyMapper = new PolicyMapper(context.OrganizationService, mappedRow, defaults);
        //        policyMapper.SetTracingService(context.TracingService);

        //        // validate row and push errors 
        //        context.Trace("Validating row.");
        //        allErrors.AddErrors(policyMapper.Validate());

        //        #region trace errors & continue to next row

        //        if (allErrors.AnyErrorsForCurrentRow())
        //        {
        //            //TraceCurrentRowErrors(context, policyMapper.PolicyNumber, allErrors);
        //            context.Trace("Skipping row {0}.".FormatWith(allErrors.RowNumber));
        //            continue;
        //        }

        //        #endregion

        //        context.Trace("Row {0} valid, starting creation of records.", allErrors.RowNumber);

        //        try
        //        {
        //            var policyHolder = policyMapper.ProcessPolicyHolder();
        //            if (policyHolder == null)
        //            {
        //                allErrors.AddError(new BordereauError(BordereauErrorType.BusinessError, "Unable to create policyholder."));
        //                continue;
        //            }

        //            var product = policyMapper.ResolveProduct();
        //            var riskClass = template.RiskClass;

        //            var policyVersion = policyMapper.CreatePolicyVersion(policyHolder, product.EntityReference, riskClass);

        //            // null policy version means re-upload of same data without changes
        //            if (policyVersion == null)
        //                continue;

        //            var riskEntity = policyMapper.ImportRiskEntity(riskClass);
        //            policyMapper.AddRiskToPolicy(policyVersion, riskEntity, riskClass.InsuredRiskLookup);

        //            #region Roles

        //            var policyVersionWrapped = new PolicyVersion(context.OrganizationService, context.TracingService, policyVersion);
        //            var policyFolderRef = policyVersionWrapped.PolicyFolder.EntityReference;

        //            foreach (var roleType in template.UniqueRoleTypeIDs)
        //            {
        //                policyMapper.AddRoleToPolicy(roleType, policyFolderRef);
        //            }

        //            #endregion
        //        }
        //        catch (BordereauException ex)
        //        {
        //            context.Trace("Bordereau Exception: {0}", ex.Message);

        //            if (ex.Error != null)
        //                allErrors.AddError(ex.Error);
        //            else
        //                allErrors.AddError(new BordereauError(BordereauErrorType.BusinessError, ex.Message));
        //        }
        //    }

        //    var allImportErrors = allErrors.AllErrors;

        //    context.Trace("{0} errors for current bordereau.", allImportErrors.Count());

        //    // if no errors are present, import done, nothing else to do
        //    if (!allImportErrors.Any())
        //        return;

        //    context.Trace("Creating error records.");

        //    foreach (var err in allImportErrors)
        //    {
        //        bxProcess.CreateErrorRecord(err);
        //    }

        //    #region Upload error

        //    MemoryStream memStream = null;
        //    using (memStream = new MemoryStream())
        //    {
        //        ErrorFileWriter.WriteErrors(allImportErrors, memStream);
        //    }

        //    var spCreds = CredentialCache.DefaultNetworkCredentials;
        //    string fileUrl = null;
        //    using (var spClient = new SharePointClient(_config["SharePointUrl"], spCreds))
        //    {
        //        // URL of SP site: https://sp.elite-insurance.co.uk:7700/
        //        // Library:        account
        //        // Folder:         [Broker new_name]/new_bordereauxprocess/[bxProcess new_name]
        //        // FileName:       [bxProcess new_name]E.csv

        //        var folderPath = "{0}/new_bordereauxprocess/{1}".FormatWith(bxProcess.Broker.GetAttributeValue<string>("new_name"), bxProcess.Name);
        //        var fileName = "{0}E.csv".FormatWith(bxProcess.Name);

        //        fileUrl = spClient.UploadFile("account", folderPath, fileName, memStream.ToArray());
        //    }

        //    var updatedProcess = new Entity(bxProcess.LogicalName);
        //    updatedProcess.Id = bxProcess.Id;
        //    updatedProcess["new_errorsfile"] = fileUrl;
        //    context.OrganizationService.Update(updatedProcess);

        //    #endregion
        //}

        private void TraceCurrentRowErrors(LocalPluginContext context, string rowIdentifier, int rowNumber, IEnumerable<BordereauError> errors)
        {
            foreach (var error in errors)
            {
                error.RecordIdentifier = rowIdentifier; // <-- refactor this side effect!!!
                context.Trace("Error: row {0}, column {1}, error {2} for value '{3}'.".FormatWith(rowNumber, error.Column.ColumnLabel, error.ErrorType, error.Value));
            }
        }

        //#region CreateClaims
        ///// <summary>
        ///// Creates the claims.
        ///// </summary>
        ///// <param name="context">The context.</param>
        ///// <param name="template">The template.</param>
        ///// <param name="importRows">The import rows.</param>
        ///// <exception cref="System.Exception">multiple claims found</exception>
        //private void CreateClaims(LocalPluginContext context, BordereauTemplate template, IEnumerable<BordereauImportRow> importRows, BordereauProcess bxProcess)
        //{
        //    ThrowIf.Argument.IsNull(context, "context");
        //    ThrowIf.Argument.IsNull(template, "template");
        //    ThrowIf.Argument.IsNull(importRows, "importrows");

        //    // Bordereau process with zero rows is ignored
        //    if (!importRows.Any())
        //        return;

        //    var templateColumns = template.TemplateColumns.ToArray();

        //    var entityName = templateColumns
        //        .Select(c => c.EntityName.ToString())
        //        .Distinct().ToList();

        //    // collection of errors in all rows
        //    var allErrors = new ErrorCollection(0);

        //    var lastRow = importRows.Count();

        //    foreach (var row in importRows)
        //    {
        //        allErrors.NextRow();

        //        var mappedRow = new MappedRow(row, template);

        //        #region validation

        //        allErrors.AddErrors(mappedRow.Attributes.ForEntity("new_claim").Validate());
        //        allErrors.AddErrors(mappedRow.Attributes.ForEntity("new_payment").Validate());
        //        allErrors.AddErrors(mappedRow.Attributes.ForEntity("new_claimrecovery").Validate());

        //        #endregion

        //        #region trace errors & continue

        //        if (allErrors.AnyErrorsForCurrentRow())
        //        {
        //            foreach (var error in allErrors.CurrentRowErrors)
        //            {
        //                context.Trace("Error: row {0}, column {1}, error {2} for value '{3}'.".FormatWith(allErrors.RowNumber, error.Column.ColumnLabel, error.ErrorType, error.Value));
        //            }

        //            context.Trace("Skipping row {0}".FormatWith(allErrors.RowNumber));
        //            foreach (var err in allErrors.AllErrors)
        //            {
        //                // TODO set claim identifier
        //                bxProcess.CreateErrorRecord(err);
        //            }
        //            continue;
        //        }

        //        #endregion

        //        #region Claim

        //        try
        //        {
        //            var claimRefValue = mappedRow.Attributes
        //                .ForEntity("new_claim")
        //                .ForAttribute("new_claimreference")
        //                .FirstOrDefault().AsString();

        //            var lossTypeField = mappedRow.Attributes
        //                .ForEntity("new_claim")
        //                .ForAttribute("new_peril1")
        //                .FirstOrDefault().AsString();

        //            var dateOfLoss = mappedRow.Attributes
        //                .ForEntity("new_claim")
        //                .ForAttribute("new_dateofloss")
        //                .FirstOrDefault().AsDateTime();

        //            var lossType = context.OrganizationService.RetrieveLossType("new_losstype", lossTypeField);

        //            var retrievedClaim = context.OrganizationService.RetrieveClaim("new_claim", claimRefValue, bxProcess.BrokerRef.Id, lossType.FirstOrDefault().Id, dateOfLoss);

        //            if (retrievedClaim.Count >= 1)
        //            {
        //                throw new InvalidPluginExecutionException("Bordereau Plugin ", new Exception("duplicate claim found"));
        //            }
        //            else
        //            {
        //                MappedClaim mapClaim = new MappedClaim();

        //                var Claim = mapClaim.MapClaim(context.OrganizationService, mappedRow, template, bxProcess);
        //            }
        //        }
        //        catch (InvalidPluginExecutionException ex)
        //        {
        //            if (ex.InnerException != null)
        //                allErrors.AddError(new BordereauError(BordereauErrorType.MissingValue, ex.InnerException.Message, ex.Message));
        //            else
        //                allErrors.AddError(new BordereauError(BordereauErrorType.MissingValue, ex.Message));
        //        }

        //        #region RecordErrors
        //        if (allErrors.RowNumber != lastRow)
        //            continue;

        //        int countError = 0;

        //        foreach (var err in allErrors.AllErrors)
        //        {
        //            var errorRecord = bxProcess.CreateErrorRecord(err);
        //            countError++;

        //            //errorRecord["new_row"] = err.Message;
        //            //errorRecord["new_errorcolumnlabel"] = err.ColumnLabel;
        //            //errorRecord["new_errorrow"] = err.RowNumber;
        //            //errorRecord["new_name"] = err.ToString();
        //            ////errorRecord["new_importid"] = new EntityReference("new_import", bxProcess.Id);
        //            //errorRecord["new_riskclassid"] = new EntityReference("new_riskclass", bxProcess.RiskClassRef.Id);

        //            //context.OrganizationService.Update(errorRecord);
        //        }

        //        Entity bordereauProcess = new Entity(bxProcess.LogicalName);
        //        bordereauProcess.Id = bxProcess.Id;
        //        bordereauProcess["new_processed"] = allErrors.RowNumber;
        //        bordereauProcess["new_errors"] = countError;
        //        bordereauProcess["new_bordereauxtemplate"] = template.Entity.ToEntityReference();
        //        context.OrganizationService.Update(bordereauProcess);

        //        int status = 100000001; // Status is Completed
        //        SetStateRequest state = new SetStateRequest();
        //        state.State = new OptionSetValue(0);
        //        state.Status = new OptionSetValue(status); //Change the Status of Bordereaux process from Load in Progress to Ready to import
        //        state.EntityMoniker = new EntityReference(bxProcess.LogicalName, bxProcess.Id);

        //        //Execute the Request
        //        context.OrganizationService.Execute(state);

        //        #endregion

        //        #region Upload error

        //        MemoryStream memStream = null;
        //        using (memStream = new MemoryStream())
        //        {
        //            ErrorFileWriter.WriteErrors(allErrors.AllErrors, memStream);
        //        }

        //        var spCreds = CredentialCache.DefaultNetworkCredentials;
        //        string fileUrl = null;
        //        using (var spClient = new SharePointClient(_config["SharePointUrl"], spCreds))
        //        {
        //            // URL of SP site: https://sp.elite-insurance.co.uk:7700/
        //            // Library:        account
        //            // Folder:         [Broker new_name]/new_bordereauxprocess/[bxProcess new_name]
        //            // FileName:       [bxProcess new_name]E.csv

        //            var folderPath = "{0}/new_bordereauxprocess/{1}".FormatWith(bxProcess.Broker.GetAttributeValue<string>("new_name"), bxProcess.Name);
        //            var fileName = "{0}E.csv".FormatWith(bxProcess.Name);

        //            fileUrl = spClient.UploadFile("account", folderPath, fileName, memStream.ToArray());
        //        }

        //        var updatedProcess = new Entity(bxProcess.LogicalName);
        //        updatedProcess.Id = bxProcess.Id;
        //        updatedProcess["new_errorsfile"] = fileUrl;
        //        context.OrganizationService.Update(updatedProcess);

        //        #endregion
        //    }
        //        #endregion Claim
        //}
        //#endregion CreateClaims

        #region Checks of status transitions

        /// <summary>
        /// Checks status transition for current operation based on pre- and post-images of Bordereau process entity.
        /// </summary>
        /// <param name="preImage">Bordereau process entity pre-image.</param>
        /// <param name="postImage">Bordereau process entity post-image.</param>
        /// <returns>True, if status transition should trigger this plug-in, otherwise false.</returns>
        private static bool CheckProcessStatusTransition(BordereauProcess preImage, BordereauProcess postImage)
        {
            // valid state transition: load in progress -> ready for import
            return preImage.Status == BordereauProcessStatus.LoadInProgress &&
                   postImage.Status == BordereauProcessStatus.ManualImport;
        }

        /// <summary>
        /// Checks status transition for current operation based on pre- and post-images of Bordereau process import row.
        /// </summary>
        /// <param name="preImage">Bordereau Import Row entity pre-image.</param>
        /// <param name="target">Bordereau Import Row entity target.</param>
        /// <returns>True, if status transition should trigger import plug-in, otherwise false.</returns>
        private static bool CheckRowStatusTransition(Entity preImage, Entity target)
        {
            var preStatus = preImage.GetAttributeValue<OptionSetValue>("statuscode");
            var postStatus = target.GetAttributeValue<OptionSetValue>("statuscode");

            if (preStatus == null || postStatus == null)
                return false;

            // valid state transition: new -> loading
            return preStatus.Value == (int)BordereauImportRowStatus.New &&
                   postStatus.Value == (int)BordereauImportRowStatus.Loading;
        }

        #endregion

        #region CreateInsuredRisks
        private IEnumerable<Entity> CreateInsuredRisks(LocalPluginContext context, Entity _policyVersion, IEnumerable<MappedAttribute> risks, PolicyMapper policyMapper)
        {
            var createdInsuredRisksList = new List<Entity>();
            //var postImage = context.PostImage;
            var policyVersion = new PolicyVersion(context.OrganizationService, context.TracingService, _policyVersion);

            var transaction = policyVersion.TransactionType;
            // insured risks are created automatically only for new policies
            if (transaction != PolicyVersionTransactionType.NewPolicy &&
                transaction != PolicyVersionTransactionType.Renewal &&
                transaction != PolicyVersionTransactionType.Cancellation) // temporary condition for Bordereau import. Cancellations shall be implemented separately. 
            {
                return Enumerable.Empty<Entity>();
            }

            var product = policyVersion.Product;
            var basePremium = policyVersion.BasePremium;

            string coverId = null;

            foreach (var risk in risks)
            {
                if (risk.Value == null)
                    continue;

                coverId = risk.TemplateColumn.CoverId;
                if (coverId == null)
                {
                    var createdInsuredRisk = CreateInsRisks(risk, context, product, policyVersion);
                    if (createdInsuredRisk != null)
                        createdInsuredRisksList.Add(createdInsuredRisk);
                    //var riskSubClass = risk.TemplateColumn.RiskSubClass;
                    //var retrievedRisk = context.OrganizationService.RetriveRiskBasedOnSubClass(riskSubClass.Id, product.Id);
                    //var retrievedRiskAttributes = new Risk(context.OrganizationService, context.TracingService, retrievedRisk.FirstOrDefault());

                    //// create only risk objects with minimum > 0
                    //if (retrievedRiskAttributes.MinimumNumberOfRisks == 0)
                    //{
                    //    var insuredRisk = new Entity("new_insuredrisk");
                    //    insuredRisk["new_product"] = product.EntityReference;
                    //    insuredRisk["new_policyid"] = policyVersion.EntityReference;

                    //    if (retrievedRiskAttributes.RiskClassRef != null)
                    //        insuredRisk["new_riskclassid"] = retrievedRiskAttributes.RiskClassRef;

                    //    if (retrievedRiskAttributes.RiskSubClassRef != null)
                    //        insuredRisk["new_secondlevelriskclass"] = retrievedRiskAttributes.RiskSubClassRef;

                    //    insuredRisk["new_riskid"] = retrievedRiskAttributes.EntityReference;

                    //    context.OrganizationService.Create(insuredRisk);

                    //}
                }
                else
                {
                    if(coverId == risk.Value)
                    {
                        var createdInsuredRisk = CreateInsRisks(risk, context, product, policyVersion);
                        if (createdInsuredRisk != null)
                            createdInsuredRisksList.Add(createdInsuredRisk);
                    }
                        
                }

                
            }
            return createdInsuredRisksList;

        }
        #endregion CreateInsuredRisks

        private Entity CreateInsRisks(MappedAttribute risk, LocalPluginContext context, Product product, PolicyVersion policyVersion)
        {
            var riskSubClass = risk.TemplateColumn.RiskSubClass;
            var retrievedRisk = context.OrganizationService.RetriveRiskBasedOnSubClass(riskSubClass.Id, product.Id);
            var retrievedRiskAttributes = new Risk(context.OrganizationService, context.TracingService, retrievedRisk.FirstOrDefault());

            // create only risk objects with minimum > 0
            if (retrievedRiskAttributes.MinimumNumberOfRisks == 0)
            {
                var insuredRisk = new Entity("new_insuredrisk");
                insuredRisk["new_product"] = product.EntityReference;
                insuredRisk["new_policyid"] = policyVersion.EntityReference;

                if (retrievedRiskAttributes.RiskClassRef != null)
                    insuredRisk["new_riskclassid"] = retrievedRiskAttributes.RiskClassRef;

                if (retrievedRiskAttributes.RiskSubClassRef != null)
                    insuredRisk["new_secondlevelriskclass"] = retrievedRiskAttributes.RiskSubClassRef;

                insuredRisk["new_riskid"] = retrievedRiskAttributes.EntityReference;

                var createdInsuredRisk = context.OrganizationService.Create(insuredRisk);

                return context.OrganizationService.Retrieve("new_insuredrisk", createdInsuredRisk, new ColumnSet(true));

            }

            return null;
        }

        private void CreateInsuredCoversForInsuredRisk(LocalPluginContext context, Entity insuredRisk, Entity cover)
        {
            //var target = context.PluginExecutionContext.InputParameters["Target"] as Entity;
            var retrievedInsuredRisk = new InsuredRisk(context.OrganizationService, context.TracingService, insuredRisk);

            var risk = retrievedInsuredRisk.Risk;
            if (risk == null)
                throw new InvalidPluginExecutionException("Insured risk requires a risk ID.");

            //foreach (var c in risk.Covers)
            //{
            //if (!c.Mandatory)
            //    continue;

            var newInsCover = new Entity("new_insuredcover");
            newInsCover["new_policyid"] = retrievedInsuredRisk.PolicyVersionRef;
            newInsCover["new_insuredriskid"] = retrievedInsuredRisk.EntityReference;
            newInsCover["new_coverid"] = cover.ToEntityReference();
            context.OrganizationService.Create(newInsCover);
            // }
        }

        private void SendEmailToUnderWriter(BordereauProcess bxProcess)
        {
            var emailId = bxProcess.ExternalUser;
            MailMessage mail = new MailMessage();
            System.Net.Mail.SmtpClient SmtpServer = new System.Net.Mail.SmtpClient("89.31.237.15 ");
            mail.From = new MailAddress("appsupport@elite-insurance.co.uk");
            //string email = "siyer@elite-insurance.co.uk,otoledo@elite-insurance.co.uk";//"siyer@elite-insurance.co.uk";
            mail.To.Add(emailId);
            //mail.To.Add("rsmart@elite-insurance.co.uk");
            //mail.To.Add("bgillis@elite-insurance.co.uk");
            //mail.CC.Add("ileeton@elite-insurance.co.uk");
            mail.Subject = "Bordereau" + " " + bxProcess.Name + "has been processed";
            mail.Body = "The above Bordereau has been processed ";
            mail.Body += Environment.NewLine;
            mail.Body += Environment.NewLine;
            mail.Body += "Please click on link below to access the Bordereau Process";
            mail.Body += Environment.NewLine;
            mail.Body += Environment.NewLine;
            //mail.Body += "https://eliteinsurance.testcrm2013.elite-insurance.co.uk:444/main.aspx?etc=10143&extraqs=&histKey=19358126&id=%7b" + createdBordereauProcessId.ToString() + "%7d&newWindow=true&pagetype=entityrecord#500658112";
            mail.Body += "https://eliteinsurance.crm.elite-insurance.co.uk:444/main.aspx?etc=10095&extraqs=&histKey=757231900&id=%7b" + bxProcess.Id.ToString() + "%7d&newWindow=true&pagetype=entityrecord#936528914";
            mail.Body += Environment.NewLine;
            mail.Body += Environment.NewLine;
            mail.Body += "Below is the link to the Bordereau Portal Report";
            mail.Body += Environment.NewLine;
            mail.Body += Environment.NewLine;
            mail.Body += "https://eliteinsurance.testcrm2013.elite-insurance.co.uk:444/crmreports/viewer/viewer.aspx?action=filter&helpID=BordereauProcess.rdl&id=%7b1468F451-01DC-E611-8102-00155D21DE18%7d";
            //mail.ReplyToList.Add("info@mygizmocover.com");

            SmtpServer.Port = 25;
            //SmtpServer.Credentials = new System.Net.NetworkCredential("administrator@elite-insurance.co.uk", "*3Lite!778");
            //SmtpServer.EnableSsl = true;
            SmtpServer.Send(mail);
        }
    }
}
