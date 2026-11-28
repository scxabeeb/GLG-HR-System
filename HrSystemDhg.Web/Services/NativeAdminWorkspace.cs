using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using HrSystemDhg.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Primitives;

namespace HrSystemDhg.Web.Services;

// Explicit allowlist: never resolve a type, method, path or SQL supplied by a client.
public static class NativeAdminWorkspace
{
    public sealed record Workspace(string Key, string Title, string Group, Type Type, Dictionary<string,string>? Defaults = null);
    static readonly Workspace[] Workspaces = [
        new("employees", "Employee directory", "HR", typeof(Pages.Hr.EmployeesModel)),
        new("register", "Register employee", "HR", typeof(Pages.Hr.RegisterModel)),
        new("employee-details", "Employee details", "HR", typeof(Pages.Hr.DetailsModel)),
        new("onboarding", "Employee onboarding", "HR", typeof(Pages.Hr.OnboardingModel)),
        new("employment", "Employment & shifts", "HR", typeof(Pages.Hr.EmploymentModel)),
        new("whatsapp", "Notifications & WhatsApp", "Administration", typeof(Pages.Admin.WhatsAppModel)),
        new("attendance", "Attendance management", "HR", typeof(Pages.Hr.AttendanceModel)),
        new("leave-balances", "Leave balances", "HR", typeof(Pages.Hr.BalancesModel)),
        new("corrections", "Attendance corrections", "HR", typeof(Pages.Hr.CorrectionsModel)),
        new("employee-history", "Employee history", "HR", typeof(Pages.Hr.HistoryEditModel)),
        new("leave", "Leave management", "HR", typeof(Pages.Hr.LeaveModel)),
        new("salary", "Salary management", "HR", typeof(Pages.Hr.SalaryModel)),
        new("payroll", "Payroll management", "HR", typeof(Pages.Management.PayrollModel)),
        new("assets", "Asset directory", "Assets", typeof(Pages.Assets.IndexModel)),
        new("asset-edit", "Register / edit asset", "Assets", typeof(Pages.Assets.EditModel)),
        new("fleet", "Fleet management", "Fleet", typeof(Pages.Operations.FleetModel)),
        new("fleet-trips", "Fleet trips", "Fleet", typeof(Pages.Operations.FleetTripsModel)),
        new("fleet-reports", "Fleet expenses & reports", "Fleet", typeof(Pages.Operations.FleetReportModel)),
        new("users", "Users & permissions", "Administration", typeof(Pages.Admin.UsersModel)),
        new("roles", "Roles & permissions", "Administration", typeof(Pages.Admin.RolesModel)),
        new("branches", "Branches", "Administration", typeof(Pages.Admin.OrgEditModel), new() { ["Kind"]="Branch" }),
        new("departments", "Departments", "Administration", typeof(Pages.Admin.OrgEditModel), new() { ["Kind"]="Department" }),
        new("sub-departments", "Sub-departments", "Administration", typeof(Pages.Admin.OrgEditModel), new() { ["Kind"]="SubDepartment" }),
        new("devices", "Attendance devices", "Administration", typeof(Pages.Admin.DevicesModel)),
        new("rules", "Attendance & deduction rules", "Administration", typeof(Pages.Admin.RulesModel)),
        new("leave-types", "Leave types", "Administration", typeof(Pages.Admin.LeaveTypesModel)),
        new("settings", "System settings", "Administration", typeof(Pages.Admin.SettingsModel)),
        new("report-views", "Report settings", "Reports", typeof(Pages.Admin.ReportViewsModel)),
        new("reports", "Main reports", "Reports", typeof(Pages.Reports.IndexModel)),
        new("audit", "Audit history", "Administration", typeof(Pages.Admin.AuditModel))
    ];
    static bool Secret(string key) => Regex.IsMatch(key, "Hash|SecurityStamp|ConcurrencyStamp|Secret|Token|ApiKey|ConnectionString|PushKey|PrivateKey|Credential|Salt", RegexOptions.IgnoreCase);
    static bool Simple(Type t) { t=Nullable.GetUnderlyingType(t)??t; return t.IsPrimitive||t.IsEnum||t==typeof(string)||t==typeof(decimal)||t==typeof(DateTime)||t==typeof(DateTimeOffset)||t==typeof(Guid); }
    static bool Handler(MethodInfo m) => m.DeclaringType != typeof(PageModel) && Regex.IsMatch(m.Name,"^On(Get|Post)[A-Z].*|^On(Get|Post)(Async)?$") && !m.Name.Contains("Signal");
    static string Label(string name) => Regex.Replace(name.Replace("Async", "").Replace("OnPost", "").Replace("OnGet", ""), "(?<=[a-z])(?=[A-Z])", " ");
    public sealed record Request(Dictionary<string,JsonElement> Values);
    public static void Map(WebApplication app)
    {
        var api=app.MapGroup("/api/mobile/v2/admin").RequireAuthorization(NativeMobileApi.Policy).RequireAuthorization(p=>p.RequireRole("Admin"));
        api.MapGet("/catalog", ()=>Results.Ok(Workspaces.Select(w=>new {w.Key,w.Title,w.Group})));
        api.MapPost("/{key}/load", (string key,Request input,HttpContext ctx)=>Execute(key,null,input,ctx));
        api.MapPost("/{key}/action/{action}", (string key,string action,Request input,HttpContext ctx)=>Execute(key,action,input,ctx));
    }
    static async Task<object?> Invoke(PageModel model,MethodInfo method,Dictionary<string,string[]> values,HttpContext ctx)
    {
        var args=new List<object?>();
        foreach(var p in method.GetParameters()) {
            object? value;
            if(p.GetCustomAttribute<FromServicesAttribute>()!=null)value=ctx.RequestServices.GetRequiredService(p.ParameterType);
            else if(p.ParameterType==typeof(CancellationToken))value=ctx.RequestAborted;
            else if(p.ParameterType==typeof(IFormFile))value=File(values,p.Name!);
            else if(Simple(p.ParameterType)||p.ParameterType.IsArray||p.ParameterType==typeof(Dictionary<string,string>))
                value=values.TryGetValue(p.Name!,out var supplied)?ConvertValue(supplied,p.ParameterType):p.HasDefaultValue?p.DefaultValue:p.ParameterType.IsValueType?Activator.CreateInstance(p.ParameterType):null;
            else {
                var prefix=p.GetCustomAttribute<BindAttribute>()?.Prefix??p.Name!;
                var metadata=ctx.RequestServices.GetRequiredService<IModelMetadataProvider>().GetMetadataForType(p.ParameterType);
                var info=new BindingInfo();
                var vp=new FormValueProvider(BindingSource.Form,new FormCollection(values.ToDictionary(v=>v.Key,v=>new StringValues(v.Value),StringComparer.OrdinalIgnoreCase)),System.Globalization.CultureInfo.InvariantCulture);
                var binding=DefaultModelBindingContext.CreateBindingContext(model.PageContext,vp,metadata,info,prefix);
                binding.Model=Activator.CreateInstance(p.ParameterType);
                await ctx.RequestServices.GetRequiredService<IModelBinderFactory>().CreateBinder(new ModelBinderFactoryContext{Metadata=metadata,BindingInfo=info,CacheToken=p}).BindModelAsync(binding);
                value=binding.Result.IsModelSet?binding.Result.Model:binding.Model;
                ctx.RequestServices.GetRequiredService<IObjectModelValidator>().Validate(model.PageContext,null,prefix,value);
            }
            args.Add(value);
        }
        foreach(var entry in model.ModelState.Values)if(entry.Errors.Count==0)entry.ValidationState=ModelValidationState.Valid;
        var result=method.Invoke(model,args.ToArray());
        if(result is Task task){await task;return task.GetType().GetProperty("Result")?.GetValue(task);}
        return result;
    }
    static object? ConvertValue(string[] values,Type type)
    {
        if(type==typeof(string[]))return values;
        if(type.IsArray && Simple(type.GetElementType()!)){var array=Array.CreateInstance(type.GetElementType()!,values.Length);for(var i=0;i<values.Length;i++)array.SetValue(ConvertValue([values[i]],type.GetElementType()!),i);return array;}
        if(type==typeof(Dictionary<string,string>)) return JsonSerializer.Deserialize<Dictionary<string,string>>(values.FirstOrDefault()??"{}");
        if(values.Length==0||string.IsNullOrEmpty(values[0]))return Nullable.GetUnderlyingType(type)!=null||!type.IsValueType?null:Activator.CreateInstance(type);
        var underlying=Nullable.GetUnderlyingType(type)??type;
        return TypeDescriptor.GetConverter(underlying).ConvertFromInvariantString(values[0]);
    }
    static IFormFile? File(Dictionary<string,string[]> values,string key)
    {
        if(!values.TryGetValue(key+".data",out var encoded)||encoded.Length==0)return null;
        var bytes=Convert.FromBase64String(encoded[0]);if(bytes.Length>5*1024*1024)throw new FormatException("Choose a file up to 5 MB.");
        var name=Path.GetFileName(values.GetValueOrDefault(key+".name")?.FirstOrDefault()??"upload");
        return new FormFile(new MemoryStream(bytes),0,bytes.Length,key,name){Headers=new HeaderDictionary(),ContentType=values.GetValueOrDefault(key+".type")?.FirstOrDefault()??"application/octet-stream"};
    }
    static async Task<IResult> Execute(string key,string? action,Request input,HttpContext ctx)
    {
        ctx.Response.Headers.CacheControl="no-store";
        var workspace=Workspaces.FirstOrDefault(w=>w.Key==key);if(workspace==null)return Results.NotFound();
        if(input.Values==null||input.Values.Count>500||ctx.Request.ContentLength>8*1024*1024)return Results.BadRequest(new {message="The request is too large."});
        var values=new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);
        foreach(var (name,value) in input.Values) values[name]=value.ValueKind==JsonValueKind.Array?value.EnumerateArray().Select(v=>v.ToString()).ToArray():[value.ToString()];
        foreach(var pair in workspace.Defaults??[])values[pair.Key]=[pair.Value];
        var model=(PageModel)ActivatorUtilities.CreateInstance(ctx.RequestServices,workspace.Type);
        model.PageContext=new PageContext{HttpContext=ctx,ActionDescriptor=new CompiledPageActionDescriptor{ViewEnginePath="/Native/"+workspace.Key,RelativePath="/Native/"+workspace.Key}};
        // Keep notices request-local; mobile actions do not create cookie TempData state.
        model.TempData=new TempDataDictionary(ctx,new MemoryTemp());
        var vp=new FormValueProvider(BindingSource.Form,new FormCollection(values.ToDictionary(p=>p.Key,p=>new StringValues(p.Value),StringComparer.OrdinalIgnoreCase)),System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            foreach(var prop in workspace.Type.GetProperties().Where(p=>p.GetCustomAttribute<BindPropertyAttribute>()!=null&&p.CanWrite))
                if(prop.PropertyType==typeof(IFormFile))prop.SetValue(model,File(values,prop.Name));
            var provider=ctx.RequestServices.GetRequiredService<IModelMetadataProvider>();
            var factory=ctx.RequestServices.GetRequiredService<IModelBinderFactory>();
            foreach(var prop in workspace.Type.GetProperties().Where(p=>p.GetCustomAttribute<BindPropertyAttribute>()!=null&&p.CanWrite&&!Secret(p.Name)&&p.PropertyType!=typeof(IFormFile)))
            {
                var metadata=provider.GetMetadataForProperty(workspace.Type,prop.Name);
                var info=new BindingInfo();
                var binding=DefaultModelBindingContext.CreateBindingContext(model.PageContext,vp,metadata,info,prop.Name);
                binding.Model=prop.GetValue(model);
                await factory.CreateBinder(new ModelBinderFactoryContext{Metadata=metadata,BindingInfo=info,CacheToken=prop}).BindModelAsync(binding);
                if(binding.Result.IsModelSet)prop.SetValue(model,binding.Result.Model);
                if(action?.StartsWith("OnPost")==true && !action.Contains("Import") && !action.Contains("Csv")) {
                    ctx.RequestServices.GetRequiredService<IObjectModelValidator>().Validate(model.PageContext,null,prop.Name,prop.GetValue(model));
                    var errors=new List<ValidationResult>();
                    Validator.TryValidateProperty(prop.GetValue(model),new ValidationContext(model){MemberName=prop.Name},errors);
                    foreach(var error in errors)model.ModelState.AddModelError(prop.Name,error.ErrorMessage??"Check this field.");
                }
            }
            foreach(var entry in model.ModelState.Values)if(entry.Errors.Count==0)entry.ValidationState=ModelValidationState.Valid;
            var methods=workspace.Type.GetMethods().Where(Handler).ToArray();
            var get=methods.FirstOrDefault(m=>m.Name is "OnGetAsync" or "OnGet");
            object? result=null;
            if(action==null){if(get!=null && !(key=="employment" && !values.ContainsKey("EmployeeId")))result=await Invoke(model,get,values,ctx);}
            else
            {
                var method=methods.FirstOrDefault(m=>m.Name==action);if(method==null)return Results.NotFound();
                if(!model.ModelState.IsValid)return Validation(model);
                result=await Invoke(model,method,values,ctx);
            }
            if(!model.ModelState.IsValid)return Validation(model);
            if(result is IStatusCodeActionResult status && status.StatusCode is >=400) {
                if(status.StatusCode==403)return Results.Forbid();
                if(status.StatusCode==404 && action==null) { /* Empty record selection still returns its native form. */ }
                else return Results.Json(new{message=result is ObjectResult obj && obj.Value is string message?message:"Check the selected record and required fields."},statusCode:status.StatusCode);
            }
            if(result is ForbidResult)return Results.Forbid();
            if(result is NotFoundResult && action!=null)return Results.NotFound();
            if(result is BadRequestResult)return Results.BadRequest(new{message="Check the selected record and required fields."});
            if(result is FileContentResult file)return Results.Ok(new{file=file.FileDownloadName,contentType=file.ContentType,data=Convert.ToBase64String(file.FileContents)});
            if(result is PhysicalFileResult physical){var bytes=await System.IO.File.ReadAllBytesAsync(physical.FileName,ctx.RequestAborted);if(bytes.Length>10*1024*1024)return Results.BadRequest(new{message="This download exceeds the mobile size limit."});return Results.Ok(new{file=physical.FileDownloadName,contentType=physical.ContentType,data=Convert.ToBase64String(bytes)});}
            if(action!=null)return Results.Ok(new{message=string.Join(" ",model.TempData.Values.OfType<string>().Where(v=>v.Length<2000)),redirect=Safe(result is RedirectToPageResult redirect?new Dictionary<string,object?>(redirect.RouteValues??[]):null)});
            var filters=workspace.Type.GetProperties().Where(p=>p.GetCustomAttribute<BindPropertyAttribute>()?.SupportsGet==true).SelectMany(p=>Fields(p.GetValue(model),p.PropertyType,p.Name,1)).Concat(get?.GetParameters().Where(p=>p.GetCustomAttribute<FromServicesAttribute>()==null).SelectMany(p=>Fields(values.TryGetValue(p.Name!,out var v)?ConvertValue(v,p.ParameterType):p.HasDefaultValue?p.DefaultValue:null,p.ParameterType,p.Name!,0))??[]).ToArray();
            var fields=workspace.Type.GetProperties().Where(p=>p.GetCustomAttribute<BindPropertyAttribute>()!=null && !(key=="register" && p.Name is ("Number" or "Pin" or "InitialPassword"))).SelectMany(p=>Fields(p.GetValue(model),p.PropertyType,p.Name,1)).Concat(get?.GetParameters().Where(p=>p.GetCustomAttribute<FromServicesAttribute>()==null).SelectMany(p=>Fields(values.TryGetValue(p.Name!,out var v)?ConvertValue(v,p.ParameterType):p.HasDefaultValue?p.DefaultValue:null,p.ParameterType,p.Name!,0))??[]).ToArray();
            var actions=methods.Where(m=>m!=get).Select(m=>new{key=m.Name,label=string.IsNullOrEmpty(Label(m.Name))?"Save":Label(m.Name),readOnly=m.Name.StartsWith("OnGet"),fields=m.GetParameters().Where(p=>p.GetCustomAttribute<FromServicesAttribute>()==null&&p.ParameterType!=typeof(CancellationToken)).SelectMany(p=>ParameterFields(model,p))});
            var db=ctx.RequestServices.GetRequiredService<HrDbContext>();
            var lookups=new Dictionary<string,object> {
                ["employees"]=await db.Employees.OrderBy(e=>e.FullName).Take(2000).Select(e=>new { value=e.Id.ToString(),label=e.FullName+" · "+e.EmployeeNumber }).ToArrayAsync(),
                ["branches"]=await db.Branchs.OrderBy(e=>e.Name).Select(e=>new { value=e.Id.ToString(),label=e.Name }).ToArrayAsync(),
                ["departments"]=await db.Departments.OrderBy(e=>e.Name).Select(e=>new { value=e.Id.ToString(),label=e.Name }).ToArrayAsync(),
                ["subDepartments"]=await db.SubDepartments.OrderBy(e=>e.Name).Select(e=>new { value=e.Id.ToString(),label=e.Name }).ToArrayAsync(),
                ["leaveTypes"]=await db.LeaveTypes.OrderBy(e=>e.Name).Select(e=>new { value=e.Id.ToString(),label=e.Name }).ToArrayAsync(),
                ["vehicles"]=await db.FleetVehicles.OrderBy(e=>e.Plate).Select(e=>new {value=e.Id.ToString(),label=e.Plate+" · "+e.Name}).ToArrayAsync(),
                ["shifts"]=await db.Shifts.OrderBy(e=>e.Name).Select(e=>new { value=e.Id.ToString(),label=e.Name }).ToArrayAsync(),
                ["devices"]=await db.ZkDevices.OrderBy(e=>e.Name).Select(e=>new { value=e.Id.ToString(),label=e.Name }).ToArrayAsync(),
                ["roles"]=await db.Roles.OrderBy(e=>e.Name).Select(e=>new { value=e.Name,label=e.Name }).ToArrayAsync(),
                ["users"]=await db.Users.OrderBy(e=>e.UserName).Take(2000).Select(e=>new {value=e.Id,label=e.UserName??e.Email??e.Id}).ToArrayAsync(),
                ["menus"]=RoleAccess.Keys.Order().Select(key=>new{value=key,label=key}).ToArray(),
                ["capabilities"]=RoleAccess.CapabilityRoles.Select(key=>new{value=key,label=key}).ToArray()
            };
            var records=Snapshot(model);
            var recordOptions=new List<object>();
            foreach(var listName in new[]{"Rows","Items","Versions","Periods","Vehicles","Trips","Days","Requests","Balances"}) {
                if(!records.TryGetValue(listName,out var collection)||collection is not object?[] rows)continue;
                foreach(var record in rows.OfType<Dictionary<string,object?>>())if(record.TryGetValue("Id",out var id)&&id!=null){var label=new[]{"FullName","Name","Title","EmployeeNumber","Email","UserName"}.Select(key=>record.GetValueOrDefault(key)?.ToString()).FirstOrDefault(v=>!string.IsNullOrWhiteSpace(v))??id.ToString();recordOptions.Add(new{value=id.ToString(),label});}
                if(recordOptions.Count>0)break;
            }
            lookups["records"]=recordOptions;
            return Results.Ok(new{workspace.Key,workspace.Title,fields,filters,actions,lookups,records,message=string.Join(" ",model.TempData.Values.OfType<string>())});
        }
        catch(Exception ex) when(ex is FormatException or ArgumentException or TargetInvocationException or InvalidOperationException)
        {
            ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("NativeAdminWorkspace").LogWarning("Native admin {Workspace} action {Action} failed: {Type}",key,action,ex.GetType().Name);
            return Results.BadRequest(new{message="Unable to save. Check required fields, record identifiers and related records, then refresh and retry."});
        }
    }
    static IResult Validation(PageModel model)=>Results.BadRequest(new{message=string.Join(" ",model.ModelState.Values.SelectMany(v=>v.Errors).Select(e=>string.IsNullOrEmpty(e.ErrorMessage)?"Check the entered value.":e.ErrorMessage).Distinct())});
    static IEnumerable<object> ParameterFields(PageModel model,ParameterInfo parameter) {
        var prefix=parameter.GetCustomAttribute<BindAttribute>()?.Prefix??parameter.Name!;
        var property=model.GetType().GetProperty(prefix,BindingFlags.Public|BindingFlags.Instance|BindingFlags.IgnoreCase);
        return Fields(property?.GetValue(model)??(parameter.HasDefaultValue?parameter.DefaultValue:null),parameter.ParameterType,prefix,1);
    }
    static string[] NamedOptions(string name) => name.Split('.').Last().ToLowerInvariant() switch {
        "operation" => ["sync","enroll","face"],
        "decision" => ["Approved","Rejected"],
        "datescope" => ["all","selected"],
        "action" => ["Submit","Approve","Reject","Reopen","Paid"],
        _=>[]
    };
    static IEnumerable<object> Fields(object? value,Type type,string name,int depth)
    {
        if(depth>3||Secret(name))yield break;
        var t=Nullable.GetUnderlyingType(type)??type;
        if(Simple(t)||typeof(IFormFile).IsAssignableFrom(t)||t.IsArray&&Simple(t.GetElementType()!)||t==typeof(List<int>)||t==typeof(Dictionary<string,string>))
        {
            yield return new{name,label=Label(name.Split('.').Last()),type=typeof(IFormFile).IsAssignableFrom(t)?"file":t==typeof(bool)?"bool":t.IsEnum?"enum":t==typeof(DateTime)||t==typeof(DateTimeOffset)?"date":name.Contains("password",StringComparison.OrdinalIgnoreCase)?"password":t==typeof(Dictionary<string,string>)?"map":t.IsArray||t==typeof(List<int>)?"list":t==typeof(int)||t==typeof(decimal)||t==typeof(double)?"number":"text",value=name.Contains("password",StringComparison.OrdinalIgnoreCase)?null:Safe(value),options=t.IsEnum?Enum.GetNames(t):NamedOptions(name),required=Nullable.GetUnderlyingType(type)==null&&type.IsValueType};yield break;
        }
        foreach(var p in t.GetProperties().Where(p=>p.CanRead&&p.CanWrite&&p.PropertyType!=typeof(HrSystemDhg.Web.Models.AssetPhoto)&&p.GetIndexParameters().Length==0&&(depth>0||p.GetCustomAttribute<BindPropertyAttribute>()!=null)))
            foreach(var field in Fields(value==null?null:p.GetValue(value),p.PropertyType,string.IsNullOrEmpty(name)?p.Name:name+"."+p.Name,depth+1))yield return field;
    }
    static Dictionary<string,object?> Snapshot(PageModel model)
    {
        var result=new Dictionary<string,object?>();
        foreach(var field in model.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance))if(!Secret(field.Name))result[field.Name]=Safe(field.GetValue(model));
        foreach(var prop in model.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p=>p.DeclaringType!=typeof(PageModel)&&p.CanRead&&p.GetIndexParameters().Length==0&&p.GetCustomAttribute<BindPropertyAttribute>()==null&&!Secret(p.Name)))result[prop.Name]=Safe(prop.GetValue(model));
        return result;
    }
    static object? Safe(object? value,int depth=0)
    {
        if(value==null||depth>4)return null;
        if(value.GetType().IsEnum)return value.ToString();
        if(Simple(value.GetType()))return value;
        if(value is IDictionary dictionary){var map=new Dictionary<string,object?>();foreach(DictionaryEntry e in dictionary){var key=e.Key.ToString()??"";if(!Secret(key))map[key]=Safe(e.Value,depth+1);if(map.Count>=200)break;}return map;}
        if(value is IEnumerable sequence)return sequence.Cast<object?>().Take(200).Select(v=>Safe(v,depth+1)).ToArray();
        return value.GetType().GetProperties().Where(p=>p.CanRead&&p.GetIndexParameters().Length==0&&!Secret(p.Name)&&p.Name is not ("AdditionalBranches" or "NormalizedEmail" or "NormalizedUserName")).ToDictionary(p=>p.Name,p=>Safe(p.GetValue(value),depth+1));
    }
    sealed class MemoryTemp:ITempDataProvider { public IDictionary<string,object> LoadTempData(HttpContext c)=>new Dictionary<string,object>();public void SaveTempData(HttpContext c,IDictionary<string,object> v){} }
}
