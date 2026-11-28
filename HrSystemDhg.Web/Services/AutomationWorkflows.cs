using System.Text.Json;
namespace HrSystemDhg.Web.Services;
public static class AutomationWorkflows
{
    public static byte[] Build(string kind){
        if(kind is not ("events" or "daily" or "reminders" or "reports"))throw new ArgumentException("Choose events, daily or reminders.");
        var nodes=new List<object>();var connections=new Dictionary<string,object>();
        object Link(string node)=>new{node,type="main",index=0};
        void Node(string name,string type,double version,object parameters,int x,int y)=>nodes.Add(new{id=Guid.NewGuid().ToString(),name,type,typeVersion=version,parameters,position=new[]{x,y}});
        if(kind=="events"){
            Node("Garowe events","n8n-nodes-base.webhook",2,new{httpMethod="POST",path="garowe-city-events",authentication="headerAuth",responseMode="onReceived",options=new{}},0,0);
            Node("Read event","n8n-nodes-base.code",2,new{jsCode="return items.map(item => ({json: item.json.body || item.json}));"},240,0);
            connections["Garowe events"]=new{main=new[]{new[]{Link("Read event")}}};
            var outputs=new List<object[]>();var rules=new List<object>();
            foreach(var module in new[]{"tasks","meetings","attendance","payroll"}){
                rules.Add(new{conditions=new{options=new{caseSensitive=true,typeValidation="strict",version=2},conditions=new[]{new{leftValue="={{ $json.module }}",rightValue=module,@operator=new{type="string",operation="equals"}}},combinator="and"},renameOutput=true,outputKey=module});
                var name=module+" — add delivery action";Node(name,"n8n-nodes-base.noOp",1,new{},720,outputs.Count*160);outputs.Add([Link(name)]);
            }
            Node("Route by module","n8n-nodes-base.switch",3.2,new{rules=new{values=rules},options=new{}},480,0);
            connections["Read event"]=new{main=new[]{new[]{Link("Route by module")}}};connections["Route by module"]=new{main=outputs};
        }else{
            var cron=kind=="daily"?"0 16 * * 0-4,6":"*/15 * * * *";
            Node("East Africa schedule","n8n-nodes-base.scheduleTrigger",1.2,new{rule=new{interval=new[]{new{field="cronExpression",expression=cron}}}},0,0);
            var links=new List<object>();
            foreach(var module in kind=="daily"?new[]{"daily"}:kind=="reports"?new[]{"tasks","meetings","attendance","payroll"}:new[]{"tasks","meetings"}){
                var read=kind=="reports"?"Read "+module:"Queue "+module+" notifications";
                if(kind=="reports")Node(read,"n8n-nodes-base.httpRequest",4.2,new{url="https://erp.garowecity.pl.so/api/integrations/n8n/summary?module="+module,authentication="genericCredentialType",genericAuthType="httpHeaderAuth",options=new{}},260,links.Count*180);
                else Node(read,"n8n-nodes-base.httpRequest",4.2,new{method="POST",url="https://erp.garowecity.pl.so/api/integrations/n8n/reminders",authentication="genericCredentialType",genericAuthType="httpHeaderAuth",sendBody=true,specifyBody="json",jsonBody=JsonSerializer.Serialize(new{kind=module}),options=new{}},260,links.Count*180);
                if(kind=="reports"){var end=module+" — add reporting action";Node(end,"n8n-nodes-base.noOp",1,new{},560,links.Count*180);connections[read]=new{main=new[]{new[]{Link(end)}}};}
                links.Add(Link(read));
            }
            connections["East Africa schedule"]=new{main=new[]{links}};
        }
        return JsonSerializer.SerializeToUtf8Bytes(new{name="Garowe City — "+kind,active=false,nodes,connections,settings=new{executionOrder="v1",timezone="Africa/Mogadishu"},meta=new{templateCredsSetupCompleted=false}},new JsonSerializerOptions{WriteIndented=true});
    }
}
