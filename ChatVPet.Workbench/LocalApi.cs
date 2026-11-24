using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

namespace ChatVPet.Workbench;

/// <summary>The desktop UI and automation call the same service; no shell commands or arbitrary file access.</summary>
public sealed class LocalApi(WorkbenchService service, int port) : IDisposable
{
    private readonly HttpListener listener = new();
    public void Start()
    {
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        _ = Listen();
    }
    private async Task Listen()
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await listener.GetContextAsync(); }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException) { break; }
            _ = Task.Run(() => Handle(context));
        }
    }
    private async Task Handle(HttpListenerContext context)
    {
        object result;
        int status = 200;
        try
        {
            if (context.Request.Headers["Origin"] != null) throw new InvalidOperationException("不接受网页来源的自动化调用。");
            var path = context.Request.Url!.AbsolutePath.Trim('/').Split('/');
            string method = context.Request.HttpMethod;
            JObject body = new();
            if (method == "POST")
            {
                if (context.Request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) != true)
                    throw new ArgumentException("POST 需要 application/json。");
                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                body = JObject.Parse(await reader.ReadToEndAsync());
            }
            if (method == "GET" && path.SequenceEqual(new[] { "api", "info" }))
                result = new { name = "ChatVPet Workbench", configs = service.Configurations(), suites = service.Scenarios.Properties().Select(x => x.Name),
                    data = service.DataDirectory, capabilities = new[] { "sessions", "steps", "stop", "retry", "compare", "notes", "export", "plugin-tools" } };
            else if (method == "GET" && path.SequenceEqual(new[] { "api", "sessions" })) result = service.List();
            else if (method == "POST" && path.SequenceEqual(new[] { "api", "sessions" }))
                result = new { id = service.Create(body.ToObject<SessionOptions>()!).Id };
            else if (method == "POST" && path.SequenceEqual(new[] { "api", "suites" }))
                result = new { ids = service.RunSuite(body.Value<string>("name")!, body["options"]?.ToObject<SessionOptions>() ?? new()) };
            else if (path.Length >= 3 && path[0] == "api" && path[1] == "sessions")
            {
                if (method == "GET" && (path.Length == 3 || path.ElementAtOrDefault(3) == "export")) result = service.Snapshot(path[2]);
                else if (method == "GET" && path.ElementAtOrDefault(3) == "events")
                {
                    int after = int.TryParse(context.Request.QueryString["after"], out int value) ? value : 0;
                    var snapshot = service.Snapshot(path[2]);
                    result = new { busy = snapshot["busy"], status = snapshot["status"], events = snapshot["events"]!.Where(x => x.Value<int>("seq") > after).ToArray() };
                }
                else if (method == "POST")
                {
                    var session = service.Get(path[2]);
                    switch (path.ElementAtOrDefault(3))
                    {
                        case "steps":
                            var steps = body["steps"]?.ToObject<List<TestStep>>() ?? [body.ToObject<TestStep>()!];
                            if (steps.Count == 0 || steps.Any(x => string.IsNullOrWhiteSpace(x.Text))) throw new ArgumentException("测试消息不能为空。");
                            session.Start(steps); result = new { accepted = true }; status = 202; break;
                        case "retry": session.Start([], true); result = new { accepted = true }; status = 202; break;
                        case "stop": session.Stop(); result = new { accepted = true }; break;
                        case "notes": session.Note(body.Value<string>("text") ?? ""); result = new { saved = true }; break;
                        case "plugin-check": result = session.CheckPlugin(); break;
                        case "compare":
                            result = new { ids = service.Compare(session.Id, body.Value<string>("text")!, body["variants"]?.ToObject<string[]>() ?? ["baseline", "no-rag"]) }; break;
                        default: throw new KeyNotFoundException("没有这个操作。");
                    }
                }
                else throw new KeyNotFoundException("没有这个接口。");
            }
            else throw new KeyNotFoundException("没有这个接口。");
        }
        catch (Exception ex)
        {
            status = ex is KeyNotFoundException ? 404 : ex is InvalidOperationException ? 409 : 400;
            result = new { error = ex.Message, detail = ex.StackTrace };
        }
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(result));
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or IOException) { }
        finally { context.Response.Close(); }
    }
    public void Dispose() => listener.Close();
}
