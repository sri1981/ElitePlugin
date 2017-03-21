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
    sealed class Agreement
    {
        IOrganizationService _svc;
        Entity _agreement = null;

        public Agreement(IOrganizationService svc, Entity agreement)
        {
            _svc = svc;
            _agreement = agreement;
        }

        public Entity AgreementEntity
        {
            get { return _agreement; }
        }

        public EntityReference Team
        {
            get { return this._agreement.GetAttributeValue<EntityReference>("new_team"); }
        }

        public EntityReference AgreementRef
        {
            get { return this._agreement.ToEntityReference(); }
        }

        public OptionSetValue StatusCode
        {
            get { 
                var statusCode = this._agreement.GetAttributeValue<OptionSetValue>("statuscode");
                if (statusCode != null)
                    return statusCode;
                return null;
            }

        }

        public Money EstimatedAnnualRevenue
        {
            get { return this._agreement.GetAttributeValue<Money>("new_estimatedannualrevenue");}
        }

        public EntityReference FirstLevelLob
        {
            get { return this._agreement.GetAttributeValue<EntityReference>("new_firstlevellobid"); }
        }

        public EntityReference FirstLevelTerritory
        {
            get { return this._agreement.GetAttributeValue<EntityReference>("new_firstlevelterritoryid"); }
        }

        public EntityReference SecondLevelLob
        {
            get { return this._agreement.GetAttributeValue<EntityReference>("new_secondlevellobid"); }
        }

        public EntityReference SecondLevelTerritory
        {
            get { return this._agreement.GetAttributeValue<EntityReference>("new_secondlevelterritoryid"); }
        }

        public EntityReference ThirdLevelLob
        {
            get { return this._agreement.GetAttributeValue<EntityReference>("new_thirdlevellobid"); }
        }

        public void AssignAgreement(IOrganizationService svc, Agreement agreement, Agreement preAgreement)
        {
            var teams = svc.RetrieveTeams(100000004);
            EntityReference firstLevelLob = null;
            EntityReference firstLevelTerritory = null;
            EntityReference secondLevelLob = null;
            EntityReference secondLevelTerritory = null;
            EntityReference thirdLevelLob = null;
            Money estimatedAnnualRevenue = null;

            if (agreement.FirstLevelLob != null)
                firstLevelLob = agreement.FirstLevelLob;
            else
                firstLevelLob = preAgreement.FirstLevelLob;
            if (agreement.FirstLevelTerritory != null)
                firstLevelTerritory = agreement.FirstLevelTerritory;
            else
                firstLevelTerritory = preAgreement.FirstLevelTerritory;
            if (agreement.SecondLevelLob != null)
                secondLevelLob = agreement.SecondLevelLob;
            else
                secondLevelLob = preAgreement.SecondLevelLob;
            if (agreement.SecondLevelTerritory != null)
                secondLevelTerritory = agreement.SecondLevelTerritory;
            else
                secondLevelTerritory = preAgreement.SecondLevelTerritory;
            if (agreement.ThirdLevelLob != null)
                thirdLevelLob = agreement.ThirdLevelLob;
            else
                thirdLevelLob = preAgreement.ThirdLevelLob;
            if (agreement.EstimatedAnnualRevenue != null)
                estimatedAnnualRevenue = agreement.EstimatedAnnualRevenue;
            else
                estimatedAnnualRevenue = preAgreement.EstimatedAnnualRevenue;

            //Comments by Sri Iyer on 16/02/2017
            //The below code is a copy and paste from PIAS plugin
            //No refactoring/changes have been made as I dont understand what is being done and the code seems to work
            #region CopyFromPiasCode
            decimal minimum = Decimal.MaxValue;
            Entity min_Team = new Entity("1");
            foreach (Entity team in teams)
            {
                EntityReference LOB = new EntityReference("asd", new Guid());
                if (team.Contains("new_lob"))
                    LOB = (EntityReference)team.Attributes["new_lob"];
                EntityReference Territory = new EntityReference("asd", new Guid());
                if (team.Contains("new_territory"))
                    Territory = (EntityReference)team["new_territory"];
                if (((LOB == null && Territory == null) || (LOB.LogicalName == "asd" && Territory.LogicalName == "asd")) || ((LOB == null || LOB.LogicalName == "asd") && (Territory.Id == firstLevelTerritory.Id || Territory.Id == secondLevelTerritory.Id)) || ((Territory == null || Territory.LogicalName == "asd") && (LOB.Id == firstLevelLob.Id || LOB.Id == secondLevelLob.Id || LOB.Id == thirdLevelLob.Id)) || ((LOB.Id == firstLevelLob.Id || LOB.Id == secondLevelLob.Id || LOB.Id == thirdLevelLob.Id) && (Territory.Id == firstLevelTerritory.Id || Territory.Id == secondLevelTerritory.Id)))
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
                //assign.Target = new EntityReference("new__agreement", old_ent.Id);
                //service.Execute(assign);
                //
                SetStateRequest setState = new SetStateRequest();
                setState.EntityMoniker = new EntityReference("new__agreement", preAgreement.AgreementEntity.Id);
                setState.State = new OptionSetValue(0);
                setState.Status = new OptionSetValue(100000000);
                SetStateResponse setStateResponse = (SetStateResponse)svc.Execute(setState);

                Entity Agreement = new Entity("new__agreement");
                Agreement.Id = preAgreement.AgreementEntity.Id;
                Agreement["new_approvalsubmitdate"] = DateTime.Now;
                Agreement["new_team"] = new EntityReference("team", min_Team.Id);
                svc.Update(Agreement);
                deactivate_approvals(svc, preAgreement.AgreementEntity.Id, "new__agreement");
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
