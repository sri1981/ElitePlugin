using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.Entities
{
    sealed class Opportunity
    {
        IOrganizationService _svc;
        Entity _opportunity = null;

        public Opportunity(IOrganizationService svc, Entity opportunity)
        {
            _svc = svc;
            _opportunity = opportunity;

        }

        public EntityReference Team
        {
            get { return this._opportunity.GetAttributeValue<EntityReference>("new_team"); }
        }

        public EntityReference OpportunityRef
        {
            get { return this._opportunity.ToEntityReference(); }
        }

        public Money EstimatedAnnualRevenue
        {
            get { return this._opportunity.GetAttributeValue<Money>("estimatedvalue"); }
        }

        public EntityReference FirstLevelLob
        {
            get { return this._opportunity.GetAttributeValue<EntityReference>("new_firstlevellob"); }
        }

        public EntityReference FirstLevelTerritory
        {
            get { return this._opportunity.GetAttributeValue<EntityReference>("new_firstlevelterritory"); }
        }

        public EntityReference SecondLevelLob
        {
            get { return this._opportunity.GetAttributeValue<EntityReference>("new_secondlevellob"); }
        }

        public EntityReference SecondLevelTerritory
        {
            get { return this._opportunity.GetAttributeValue<EntityReference>("new_secondlevelterritory"); }
        }

        public EntityReference ThirdLevelLob
        {
            get { return this._opportunity.GetAttributeValue<EntityReference>("new_thirdlevellob"); }
        }

        public void AssignOpportunity(IOrganizationService svc, Opportunity opportunity, Opportunity preOpportunity)
        {
            var teams = svc.RetrieveTeams(100000007);
            EntityReference firstLevelLob = null;
            EntityReference firstLevelTerritory = null;
            EntityReference secondLevelLob = null;
            EntityReference secondLevelTerritory = null;
            EntityReference thirdLevelLob = null;
            Money estimatedAnnualRevenue = null;

            if (opportunity.FirstLevelLob != null)
                firstLevelLob = opportunity.FirstLevelLob;
            else
                firstLevelLob = preOpportunity.FirstLevelLob;
            if (opportunity.FirstLevelTerritory != null)
                firstLevelTerritory = opportunity.FirstLevelTerritory;
            else
                firstLevelTerritory = preOpportunity.FirstLevelTerritory;
            if (opportunity.SecondLevelLob != null)
                secondLevelLob = opportunity.SecondLevelLob;
            else
                secondLevelLob = preOpportunity.SecondLevelLob;
            if (opportunity.SecondLevelTerritory != null)
                secondLevelTerritory = opportunity.SecondLevelTerritory;
            else
                secondLevelTerritory = preOpportunity.SecondLevelTerritory;
            if (opportunity.ThirdLevelLob != null)
                thirdLevelLob = opportunity.ThirdLevelLob;
            else
                thirdLevelLob = preOpportunity.ThirdLevelLob;
            if (opportunity.EstimatedAnnualRevenue != null)
                estimatedAnnualRevenue = opportunity.EstimatedAnnualRevenue;
            else
                estimatedAnnualRevenue = preOpportunity.EstimatedAnnualRevenue;

            //Comments by Sri Iyer on 16/02/2017
            //The below code is a copy and paste from PIAS plugin
            //No refactoring/changes have been made as I dont understand what is being done and the code seems to work

            #region CopyFromPiasCode
            decimal minimum = Decimal.MaxValue;
            Entity min_Team = new Entity("1");

            foreach(var team in teams)
            {
                var LOB = team.GetAttributeValue<EntityReference>("new_lob");

            }

            foreach (Entity team in teams)
            {
                EntityReference LOB = new EntityReference("asd", new Guid());
                if (team.Contains("new_lob"))
                    LOB = (EntityReference)team.Attributes["new_lob"];
                EntityReference Territory = new EntityReference("asd", new Guid());
                if (team.Contains("new_territory"))
                    Territory = (EntityReference)team["new_territory"];
                if (((LOB == null && Territory == null) || (LOB.LogicalName == "asd" && Territory.LogicalName == "asd"))
                    || ((LOB == null || LOB.LogicalName == "asd") && (Territory.Id == firstLevelTerritory.Id || Territory.Id == secondLevelTerritory.Id)) 
                    || ((Territory == null || Territory.LogicalName == "asd") && (LOB.Id == firstLevelLob.Id || LOB.Id == secondLevelLob.Id || LOB.Id == thirdLevelLob.Id)) 
                    || ((LOB.Id == firstLevelLob.Id || LOB.Id == secondLevelLob.Id || LOB.Id == thirdLevelLob.Id) 
                    && (Territory.Id == firstLevelTerritory.Id || Territory.Id == secondLevelTerritory.Id)))
                {
                    //matching_teams.Add(team);
                    Money limit = (Money)team.Attributes["new_aprovallimit"];
                    if (limit.Value < minimum && limit.Value >= estimatedAnnualRevenue.Value)
                    {
                        minimum = limit.Value;
                        min_Team = team;
                    }
                }

            }
            if (min_Team.LogicalName != "1")
            {
                //AssignRequest assign = new AssignRequest();
                //assign.Assignee = new EntityReference("team", min_Team.Id);
                //assign.Target = new EntityReference("opportunity", old_ent.Id);
                //service.Execute(assign);
                Entity Opportunity = new Entity("opportunity");
                Opportunity.Id = preOpportunity.OpportunityRef.Id;
                Opportunity["new_team"] = new EntityReference("team", min_Team.Id);
                Opportunity["new_approvalstatus"] = null;
                svc.Update(Opportunity);
                deactivate_approvals(svc, preOpportunity.OpportunityRef.Id, "new_opportunity");
                //throw new InvalidPluginExecutionException("You are not a member of the approval team.");
            }
            #endregion
        }

        static void deactivate_approvals(IOrganizationService service, Guid attributeID, string attributeName)
        {
            QueryExpression qe = new QueryExpression
            {
                EntityName = "new_approval",
                ColumnSet = new ColumnSet("new_approvalid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                {
                  new ConditionExpression
                  {
                    AttributeName = attributeName,
                    Operator = ConditionOperator.Equal,
                    Values = { attributeID }
                  },
                  new ConditionExpression
                  {
                    AttributeName = "statecode",
                    Operator = ConditionOperator.Equal,
                    Values = { 0 }
                  }
                }
                }
            };
            DataCollection<Entity> approvals = service.RetrieveMultiple(qe).Entities;
            foreach (Entity approval in approvals)
            {

                SetStateRequest setState = new SetStateRequest();
                setState.EntityMoniker = new EntityReference("new_approval", (Guid)approval.Attributes["new_approvalid"]);
                setState.State = new OptionSetValue(1);
                setState.Status = new OptionSetValue(2);
                SetStateResponse setStateResponse = (SetStateResponse)service.Execute(setState);
            }

        }
    }
}
