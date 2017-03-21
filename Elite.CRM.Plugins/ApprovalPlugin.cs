using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elite.CRM.Plugins.Entities;
using Microsoft.Crm.Sdk.Messages;

namespace Elite.CRM.Plugins
{
    public class ApprovalPlugin : BasePlugin
    {
        public ApprovalPlugin(string unsecureConfig, string secureConfig)
            : base(unsecureConfig, secureConfig)
        {
            RegisterEvent(PluginStage.PostOperation, PluginMessage.Create, "new_approval", ApprovalPostCreate);
            RegisterEvent(PluginStage.PostOperation, PluginMessage.Update, "new_approval", ApprovalUpdate);
        }

        public enum ApprovalEntity
        {
            Policy = 100000000,
            Claim = 100000001,
            Payment = 100000002,
            Cover = 100000003,
            Opportunity = 100000007,
            Agreement = 100000004,
            ReinsuranceAgreement = 100000005,
            Broker = 100000006,
            Product = 100000008
        }

        public enum ApprovalAction
        {
            Approved = 100000000,
            Pending = 100000001,
            Rejected = 100000002
        }

        private void ApprovalPostCreate(LocalPluginContext context)
        {
            var svc = context.OrganizationService;
            var approval = context.PluginExecutionContext.InputParameters["Target"] as Entity;

            var approvalDetails = new Approval(svc, approval);

            if (approvalDetails.CheckApprovalAssigned(svc))
            {
                var agreement = approvalDetails.Agreement;
                var opportunity = approvalDetails.Opportunity;

                if ((ApprovalEntity)approvalDetails.ApprovalEntity.Value == ApprovalEntity.Agreement/*Agreement*/ && 
                    (ApprovalAction) approvalDetails.ApprovalAction.Value == ApprovalAction.Approved)
                        approvalDetails.CheckApproval(svc, approvalDetails, context.PluginExecutionContext.UserId, approvalDetails.AgreementRef.LogicalName);
                else if ((ApprovalEntity)approvalDetails.ApprovalEntity.Value == ApprovalEntity.Opportunity/*Opportunity*/ &&
                    (ApprovalAction)approvalDetails.ApprovalAction.Value == ApprovalAction.Approved)
                        approvalDetails.CheckApproval(svc, approvalDetails, context.PluginExecutionContext.UserId, approvalDetails.AgreementRef.LogicalName);
                else if ((ApprovalEntity)approvalDetails.ApprovalEntity.Value == ApprovalEntity.Agreement/*Agreement*/ &&
                    (ApprovalAction)approvalDetails.ApprovalAction.Value == ApprovalAction.Rejected)
                        approvalDetails.RejectAgreement(svc, approvalDetails, context.PluginExecutionContext.UserId);
                else if ((ApprovalEntity)approvalDetails.ApprovalEntity.Value == ApprovalEntity.Opportunity/*Opportunity*/ &&
                    (ApprovalAction)approvalDetails.ApprovalAction.Value == ApprovalAction.Rejected)
                        approvalDetails.RejectOpportunity(svc, approvalDetails, context.PluginExecutionContext.UserId);
            }
            
        }

        private void ApprovalUpdate(LocalPluginContext context)
        {
            var svc = context.OrganizationService;
            var approval = new Approval(svc, context.PluginExecutionContext.InputParameters["Target"] as Entity);
            var preApproval = new Approval(svc, context.PreImage);
            OptionSetValue approvalEntity = null;
            OptionSetValue approvalAction = null;
            var agreement = approval.Agreement;
            var opportunity = approval.Opportunity;

            if (approval.ApprovalEntity != null)
                approvalEntity = approval.ApprovalEntity;
            else
                approvalEntity = preApproval.ApprovalEntity;

            if (approval.ApprovalAction != null)
                approvalAction = approval.ApprovalAction;
            else
                approvalAction = preApproval.ApprovalAction;

            if ((ApprovalEntity)approvalEntity.Value == ApprovalEntity.Agreement &&
                (ApprovalAction)approvalAction.Value == ApprovalAction.Approved)
                approval.CheckApproval(svc, approval, context.PluginExecutionContext.UserId, approval.AgreementRef.LogicalName);
            else if ((ApprovalEntity)approvalEntity.Value == ApprovalEntity.Agreement &&
                (ApprovalAction)approvalAction.Value == ApprovalAction.Rejected)
                approval.RejectAgreement(svc, approval, context.PluginExecutionContext.UserId);
            else if ((ApprovalEntity)approvalEntity.Value == ApprovalEntity.Opportunity &&
                (ApprovalAction)approvalAction.Value == ApprovalAction.Approved)
                approval.CheckApproval(svc, approval, context.PluginExecutionContext.UserId, approval.OpportunityRef.LogicalName);
            else if ((ApprovalEntity)approvalEntity.Value == ApprovalEntity.Opportunity &&
                (ApprovalAction)approvalAction.Value == ApprovalAction.Rejected)
                approval.RejectOpportunity(svc, approval, context.PluginExecutionContext.UserId);

            if ((ApprovalAction)approvalAction.Value == ApprovalAction.Approved ||
                (ApprovalAction)approvalAction.Value == ApprovalAction.Rejected)
                approval.SetApprovalStatus(svc, approval._Approval);
        }

        //private void CheckApprovalAssigned(IOrganizationService svc, Entity approval)
        //{
        //    if (approval.Contains("new_approvalentity") && approval.Contains("new_approvalaction"))
        //    {
        //        if (approval.Contains("new__agreement"))
        //        {
        //            var agreementRef = approval.GetAttributeValue<EntityReference>("new__agreement");
        //            if (agreementRef != null)
        //            {
        //                var agreement = svc.Retrieve(agreementRef.LogicalName, agreementRef.Id, new ColumnSet(true));
        //                if (!agreement.Contains("new_team"))
        //                {
        //                    throw new InvalidPluginExecutionException("The agreement has not been assigned to an approval team.");
        //                }
        //            }
        //        }

        //        if (approval.Contains("new_opportunity"))
        //        {
        //            var opportunityRef = approval.GetAttributeValue<EntityReference>("new_opportunity");
        //            if (opportunityRef != null)
        //            {
        //                var opportunity = svc.Retrieve(opportunityRef.LogicalName, opportunityRef.Id, new ColumnSet(true));
        //                if (!opportunity.Contains("new_team"))
        //                    throw new InvalidPluginExecutionException("The opportunity has not been assigned to an approval team.");
        //            }
        //        }
        //    }
        //}

        //private void CheckApprovalOnCreate(IOrganizationService svc, Approval approvalEntity, Guid userId, string approvalEntityName)
        //{
        //    if (approvalEntityName == "new__agreement")
        //    {
        //        if (svc.RetrieveApprovalsPerUser(approvalEntity.AgreementRef.LogicalName, approvalEntity.AgreementRef.Id, userId) > 1)
        //            throw new InvalidPluginExecutionException("You can perform only 1 approval action for this record.");

        //        var agreement = approvalEntity.Agreement;
        //        var agreementRef = approvalEntity.AgreementRef;
        //        if (agreement.Team == null)
        //        {
        //            if (!svc.CheckUser(userId, agreement.Team.Id))
        //                throw new InvalidPluginExecutionException("You are not a member of the approval team.");

        //            var team = svc.Retrieve(agreement.Team.LogicalName, agreement.Team.Id, new ColumnSet(true));

        //            UpdateEntity(svc, agreementRef, team);

        //            //var approvalRequired = team.GetAttributeValue<int>("new_approvalrequired");
        //            //var approvalCount = svc.RetriveApprovalCount(approvalEntity.AgreementRef.LogicalName, approvalEntity.AgreementRef.Id);

        //            //if(approvalRequired != approvalCount)
        //            //    throw new InvalidPluginExecutionException("You are not a member of the approval team.");

        //            //SetStateRequest setState = new SetStateRequest();
        //            //setState.EntityMoniker = approvalEntity.AgreementRef;
        //            //setState.State = new OptionSetValue(0);
        //            //setState.Status = new OptionSetValue(100000001);
        //            //SetStateResponse setStateResponse = (SetStateResponse)svc.Execute(setState);

        //            //Entity Agr = new Entity(approvalEntity.AgreementRef.LogicalName);
        //            //Agr.Id = approvalEntity.AgreementRef.Id;
        //            //Agr["new_approvaldate"] = DateTime.Now;
        //            //svc.Update(Agr);
        //        }
        //    }
        //    else if (approvalEntityName == "opportunity")
        //    {
        //        var opportunity = approvalEntity.Opportunity;
        //        var oppRef = approvalEntity.OpportunityRef;
        //        if (svc.RetrieveApprovalsPerUser("new_opportunity", oppRef.Id, userId) > 1)
        //            throw new InvalidPluginExecutionException("You can perform only 1 approval action for this record.");

        //        if (!svc.CheckUser(userId, approvalEntity.Opportunity.Team.Id))
        //            throw new InvalidPluginExecutionException("You are not a member of the approval team.");

        //        var team = svc.Retrieve(opportunity.Team.LogicalName, opportunity.Team.Id, new ColumnSet(true));
        //        UpdateEntity(svc, oppRef, team);
        //        //var approvalRequired = team.GetAttributeValue<int>("new_approvalrequired");
        //        //var approvalCount = svc.RetriveApprovalCount(oppRef.LogicalName, oppRef.Id);
        //        //if (approvalRequired != approvalCount)
        //        //    throw new InvalidPluginExecutionException("You are not a member of the approval team.");

        //        //Entity Opp = new Entity(oppRef.LogicalName);
        //        //Opp.Id = oppRef.Id;
        //        //Opp["new_approvaldate"] = DateTime.Now;
        //        //Opp["new_approvalstatus"] = new OptionSetValue(100000000);
        //        //svc.Update(Opp);

        //    }
        //}

        //private void UpdateEntity(IOrganizationService svc, EntityReference entityRef, Entity team)
        //{
        //    var approvalRequired = team.GetAttributeValue<int>("new_approvalrequired");
        //    var approvalCount = svc.RetriveApprovalCount(entityRef.LogicalName, entityRef.Id);
        //    if (approvalRequired != approvalCount)
        //        throw new InvalidPluginExecutionException("You are not a member of the approval team.");

        //    if(entityRef.LogicalName == "new__agreement")
        //    {
        //        SetStateRequest setState = new SetStateRequest();
        //        setState.EntityMoniker = entityRef;
        //        setState.State = new OptionSetValue(0);
        //        setState.Status = new OptionSetValue(100000001);
        //        SetStateResponse setStateResponse = (SetStateResponse)svc.Execute(setState);
        //    }

        //    Entity entity = new Entity(entityRef.LogicalName);
        //    entity.Id = entityRef.Id;
        //    entity["new_approvaldate"] = DateTime.Now;
        //    if (entityRef.LogicalName == "opportunity")
        //        entity["new_approvalstatus"] = new OptionSetValue(100000000);
        //    svc.Update(entity);
        //}

        //private void RejectAgreement(LocalPluginContext context, Approval approvalEntity)
        //{
        //    var svc = context.OrganizationService;
        //    var userId = context.PluginExecutionContext.UserId;
        //    var agreementRef = approvalEntity.AgreementRef;
        //    var teamRef = approvalEntity.Agreement.Team;
        //    if(svc.RetrieveApprovalsPerUser("new__agreement", agreementRef.Id, userId) > 1)
        //        throw new InvalidPluginExecutionException("You can perform only 1 approval action for this record.");

        //    if(teamRef != null)
        //    {
        //        if(!svc.CheckUser(userId, teamRef.Id))
        //            throw new InvalidPluginExecutionException("You are not a member of the approval team.");

        //        var team = svc.Retrieve(teamRef.LogicalName, teamRef.Id, new ColumnSet(true));
        //        var rejectionRequired = team.GetAttributeValue<int>("new_rejectionrequired");
        //        var currentRejectionCount = svc.RetriveRejectionCounts(agreementRef.LogicalName, agreementRef.Id);

        //        if(rejectionRequired == currentRejectionCount)
        //        {
        //            SetStateRequest setState = new SetStateRequest();
        //            setState.EntityMoniker = agreementRef;
        //            setState.State = new OptionSetValue(1);
        //            setState.Status = new OptionSetValue(100000006);
        //            SetStateResponse setStateResponse = (SetStateResponse)svc.Execute(setState);
        //            Entity Agr = new Entity("new__agreement");
        //            Agr.Id = agreementRef.Id;
        //            Agr["new_rejectiondate"] = DateTime.Now;
        //            svc.Update(Agr);
        //        }

        //    }
        //}

        //private void RejectOpportunity(LocalPluginContext context, Approval approvalEntity)
        //{
        //    var svc = context.OrganizationService;
        //    var userId = context.PluginExecutionContext.UserId;
        //    var OpportunityRef = approvalEntity.OpportunityRef;
        //    var teamRef = approvalEntity.Opportunity.Team;
        //    if (svc.RetrieveApprovalsPerUser("new_opportunity", OpportunityRef.Id, userId) > 1)
        //        throw new InvalidPluginExecutionException("You can perform only 1 approval action for this record.");

        //    if (teamRef != null)
        //    {
        //        if (!svc.CheckUser(userId, teamRef.Id))
        //            throw new InvalidPluginExecutionException("You are not a member of the approval team.");

        //        var team = svc.Retrieve(teamRef.LogicalName, teamRef.Id, new ColumnSet(true));
        //        var rejectionRequired = team.GetAttributeValue<int>("new_rejectionrequired");
        //        var currentRejectionCount = svc.RetriveRejectionCounts(OpportunityRef.LogicalName, OpportunityRef.Id);

        //        if (rejectionRequired == currentRejectionCount)
        //        {
        //            Entity Opp = new Entity("opportunity");
        //            Opp.Id = OpportunityRef.Id;
        //            Opp["new_rejectiondate"] = DateTime.Now;
        //            Opp["new_approvalstatus"] = new OptionSetValue(100000002);
        //            svc.Update(Opp);
        //        }

        //    }


        //}
    }
}
