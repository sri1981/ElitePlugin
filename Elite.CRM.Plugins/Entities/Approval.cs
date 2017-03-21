using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Crm.Sdk.Messages;


namespace Elite.CRM.Plugins.Entities
{
    sealed class Approval
    {
        IOrganizationService _svc;
        Entity _approval = null;

        public Approval(IOrganizationService svc, Entity approval)
        {
            _svc = svc;
            _approval = approval;
        }

        public Entity _Approval
        {
            get { return _approval; }
        }

        public OptionSetValue ApprovalEntity
        {
            get 
            { 
                var approve = this._approval.GetAttributeValue<OptionSetValue>("new_approvalentity");
                if (approve != null)
                    return approve;

                return null;
            }
        }

        public OptionSetValue ApprovalAction
        {
            get 
            { 
                var action = this._approval.GetAttributeValue<OptionSetValue>("new_approvalaction");
                if (action != null)
                    return action;

                return null;
            }
        }

        public EntityReference AgreementRef
        {
            get { return this._approval.GetAttributeValue<EntityReference>("new__agreement");}
        }

        public EntityReference OpportunityRef
        {
            get { return this._approval.GetAttributeValue<EntityReference>("new_opportunity"); }
        }

        public Agreement Agreement
        {
            get
            {
                return new Agreement(_svc, _svc.Retrieve(AgreementRef.LogicalName, AgreementRef.Id, new ColumnSet(true)));
            }
        }

        public Opportunity Opportunity
        {
            get
            {
                return new Opportunity(_svc, _svc.Retrieve(OpportunityRef.LogicalName, OpportunityRef.Id, new ColumnSet(true)));
            }
        }

        public bool CheckApprovalAssigned(IOrganizationService svc) //Entity approval
        {
            if (this._approval.Contains("new_approvalentity") && this._approval.Contains("new_approvalaction"))
            {
                if (this._approval.Contains("new__agreement"))
                {
                    var agreementRef = this.AgreementRef;//approval.GetAttributeValue<EntityReference>("new__agreement");
                    if (agreementRef != null)
                    {
                        var agreement = svc.Retrieve(agreementRef.LogicalName, agreementRef.Id, new ColumnSet(true));
                        if (!agreement.Contains("new_team"))
                        {
                            throw new InvalidPluginExecutionException("The agreement has not been assigned to an approval team.");
                        }
                    }
                    return true;
                }

                if (this._approval.Contains("new_opportunity"))
                {
                    var opportunityRef = this.OpportunityRef;//this._approval.GetAttributeValue<EntityReference>("new_opportunity");
                    if (opportunityRef != null)
                    {
                        var opportunity = svc.Retrieve(opportunityRef.LogicalName, opportunityRef.Id, new ColumnSet(true));
                        if (!opportunity.Contains("new_team"))
                            throw new InvalidPluginExecutionException("The opportunity has not been assigned to an approval team.");
                    }

                    return true;
                }
            }

            return false;
        }

        public void CheckApproval(IOrganizationService svc, Approval approvalEntity, Guid userId, string approvalEntityName)
        {
            if (approvalEntityName == "new__agreement")
            {
                if (svc.RetrieveApprovalsPerUser(approvalEntity.AgreementRef.LogicalName, approvalEntity.AgreementRef.Id, userId) > 1)
                    throw new InvalidPluginExecutionException("You can perform only 1 approval action for this record.");

                var agreement = approvalEntity.Agreement;
                var agreementRef = approvalEntity.AgreementRef;
                if (agreement.Team == null)
                {
                    if (!svc.CheckUser(userId, agreement.Team.Id))
                        throw new InvalidPluginExecutionException("You are not a member of the approval team.");

                    var team = svc.Retrieve(agreement.Team.LogicalName, agreement.Team.Id, new ColumnSet(true));

                    UpdateEntity(svc, agreementRef, team);

                    //var approvalRequired = team.GetAttributeValue<int>("new_approvalrequired");
                    //var approvalCount = svc.RetriveApprovalCount(approvalEntity.AgreementRef.LogicalName, approvalEntity.AgreementRef.Id);

                    //if(approvalRequired != approvalCount)
                    //    throw new InvalidPluginExecutionException("You are not a member of the approval team.");

                    //SetStateRequest setState = new SetStateRequest();
                    //setState.EntityMoniker = approvalEntity.AgreementRef;
                    //setState.State = new OptionSetValue(0);
                    //setState.Status = new OptionSetValue(100000001);
                    //SetStateResponse setStateResponse = (SetStateResponse)svc.Execute(setState);

                    //Entity Agr = new Entity(approvalEntity.AgreementRef.LogicalName);
                    //Agr.Id = approvalEntity.AgreementRef.Id;
                    //Agr["new_approvaldate"] = DateTime.Now;
                    //svc.Update(Agr);
                }
            }
            else if (approvalEntityName == "opportunity")
            {
                var opportunity = approvalEntity.Opportunity;
                var oppRef = approvalEntity.OpportunityRef;
                if (svc.RetrieveApprovalsPerUser("new_opportunity", oppRef.Id, userId) > 1)
                    throw new InvalidPluginExecutionException("You can perform only 1 approval action for this record.");

                if (!svc.CheckUser(userId, approvalEntity.Opportunity.Team.Id))
                    throw new InvalidPluginExecutionException("You are not a member of the approval team.");

                var team = svc.Retrieve(opportunity.Team.LogicalName, opportunity.Team.Id, new ColumnSet(true));
                UpdateEntity(svc, oppRef, team);
                //var approvalRequired = team.GetAttributeValue<int>("new_approvalrequired");
                //var approvalCount = svc.RetriveApprovalCount(oppRef.LogicalName, oppRef.Id);
                //if (approvalRequired != approvalCount)
                //    throw new InvalidPluginExecutionException("You are not a member of the approval team.");

                //Entity Opp = new Entity(oppRef.LogicalName);
                //Opp.Id = oppRef.Id;
                //Opp["new_approvaldate"] = DateTime.Now;
                //Opp["new_approvalstatus"] = new OptionSetValue(100000000);
                //svc.Update(Opp);

            }
        }

        private void UpdateEntity(IOrganizationService svc, EntityReference entityRef, Entity team)
        {
            var approvalRequired = team.GetAttributeValue<int>("new_approvalrequired");
            var approvalCount = svc.RetriveApprovalCount(entityRef.LogicalName, entityRef.Id);
            if (approvalRequired != approvalCount)
                throw new InvalidPluginExecutionException("You are not a member of the approval team.");

            if (entityRef.LogicalName == "new__agreement")
            {
                SetStateRequest setState = new SetStateRequest();
                setState.EntityMoniker = entityRef;
                setState.State = new OptionSetValue(0);
                setState.Status = new OptionSetValue(100000001);
                SetStateResponse setStateResponse = (SetStateResponse)svc.Execute(setState);
            }

            Entity entity = new Entity(entityRef.LogicalName);
            entity.Id = entityRef.Id;
            entity["new_approvaldate"] = DateTime.Now;
            if (entityRef.LogicalName == "opportunity")
                entity["new_approvalstatus"] = new OptionSetValue(100000000);
            svc.Update(entity);
        }
        public void RejectAgreement(IOrganizationService svc, Approval approvalEntity, Guid userId)
        {
            //var svc = context.OrganizationService;
            //var userId = context.PluginExecutionContext.UserId;
            var agreementRef = approvalEntity.AgreementRef;
            var teamRef = approvalEntity.Agreement.Team;
            if (svc.RetrieveApprovalsPerUser("new__agreement", agreementRef.Id, userId) > 1)
                throw new InvalidPluginExecutionException("You can perform only 1 approval action for this record.");

            if (teamRef != null)
            {
                if (!svc.CheckUser(userId, teamRef.Id))
                    throw new InvalidPluginExecutionException("You are not a member of the approval team.");

                var team = svc.Retrieve(teamRef.LogicalName, teamRef.Id, new ColumnSet(true));
                var rejectionRequired = team.GetAttributeValue<int>("new_rejectionrequired");
                var currentRejectionCount = svc.RetriveRejectionCounts(agreementRef.LogicalName, agreementRef.Id);

                if (rejectionRequired == currentRejectionCount)
                {
                    SetStateRequest setState = new SetStateRequest();
                    setState.EntityMoniker = agreementRef;
                    setState.State = new OptionSetValue(1);
                    setState.Status = new OptionSetValue(100000006);
                    SetStateResponse setStateResponse = (SetStateResponse)svc.Execute(setState);
                    Entity Agr = new Entity("new__agreement");
                    Agr.Id = agreementRef.Id;
                    Agr["new_rejectiondate"] = DateTime.Now;
                    svc.Update(Agr);
                }

            }
        }

        public void RejectOpportunity(IOrganizationService svc, Approval approvalEntity, Guid userId)
        {
            //var svc = context.OrganizationService;
            //var userId = context.PluginExecutionContext.UserId;
            var OpportunityRef = approvalEntity.OpportunityRef;
            var teamRef = approvalEntity.Opportunity.Team;
            if (svc.RetrieveApprovalsPerUser("new_opportunity", OpportunityRef.Id, userId) > 1)
                throw new InvalidPluginExecutionException("You can perform only 1 approval action for this record.");

            if (teamRef != null)
            {
                if (!svc.CheckUser(userId, teamRef.Id))
                    throw new InvalidPluginExecutionException("You are not a member of the approval team.");

                var team = svc.Retrieve(teamRef.LogicalName, teamRef.Id, new ColumnSet(true));
                var rejectionRequired = team.GetAttributeValue<int>("new_rejectionrequired");
                var currentRejectionCount = svc.RetriveRejectionCounts(OpportunityRef.LogicalName, OpportunityRef.Id);

                if (rejectionRequired == currentRejectionCount)
                {
                    Entity Opp = new Entity("opportunity");
                    Opp.Id = OpportunityRef.Id;
                    Opp["new_rejectiondate"] = DateTime.Now;
                    Opp["new_approvalstatus"] = new OptionSetValue(100000002);
                    svc.Update(Opp);
                }

            }


        }

        public void SetApprovalStatus(IOrganizationService service, Entity approval)
        {
            SetStateRequest req = new SetStateRequest();

            req.EntityMoniker = new EntityReference(approval.LogicalName, approval.Id);
            req.State = new OptionSetValue(1);
            req.Status = new OptionSetValue(2);

            SetStateResponse resp = (SetStateResponse)service.Execute(req);


        }


    }
}
