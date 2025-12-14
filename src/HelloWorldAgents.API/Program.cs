using HelloWorldAgents.API;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Uncomment to use GitHub Models
//builder.AddOpenAIClient("chat", settings =>
//    {
//        settings.Endpoint = new Uri("https://models.github.ai/inference");
//        settings.EnableSensitiveTelemetryData = true;
//    })
//    .AddChatClient(Environment.GetEnvironmentVariable("MODEL_NAME")!);

builder.Services.AddSingleton(sp =>
{
    return new ChatClient(
            "gpt-4o-mini",
            new ApiKeyCredential(Environment.GetEnvironmentVariable("GITHUB_TOKEN")!),
            new OpenAIClientOptions { Endpoint = new Uri("https://models.github.ai/inference") })
        .AsIChatClient();
});

builder.AddAIAgent("Writer", (sp, key) =>
{
    var chatClient = sp.GetRequiredService<IChatClient>();
    return new ChatClientAgent(
        chatClient,
        name: key,
        instructions:
            """
            You are a creative writing assistant who crafts vivid, 
            well-structured stories with compelling characters based on user prompts, 
            and formats them after writing.
            """,
        tools: [
            AIFunctionFactory.Create(GetAuthor),
            AIFunctionFactory.Create(FormatStory)
        ]
    );
});

builder.AddAIAgent(
    name: "Editor",
    instructions:
        """
        You are an editor who improves a writer’s draft by providing 4–8 concise recommendations and 
        a fully revised Markdown document, focusing on clarity, coherence, accuracy, and alignment.
        """);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Enable static file serving from wwwroot
    app.UseStaticFiles();
}


app.UseHttpsRedirection();

app.MapGet("/agent/chat", async (
    [FromKeyedServices("Writer")] AIAgent writer,
    [FromKeyedServices("Editor")] AIAgent editor,
    HttpContext context,
    string prompt) =>
{
    Workflow workflow =
        AgentWorkflowBuilder
            .CreateGroupChatBuilderWith(agents =>
                new RoundRobinRoutingPolicy(agents)
                {
                    MaximumIterationCount = 3
                })
            .AddParticipants(writer, editor)
            .Build();

    AIAgent workflowAgent = workflow.AsAgent();

    AgentRunResponse response = await workflowAgent.RunAsync(prompt);
    return Results.Ok(response);
});

app.MapDefaultEndpoints();

app.Run();

[Description("Gets the author of the story.")]
string GetAuthor() => "Jack Torrance";

[Description("Formats the story for display.")]
string FormatStory(string title, string author, string story) =>
    $"Title: {title}\nAuthor: {author}\n\n{story}";