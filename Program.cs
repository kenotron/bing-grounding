using Azure;
using Azure.Identity;
using Azure.AI.Projects;
using Azure.AI.Agents.Persistent;
using Microsoft.Extensions.Configuration;

async Task RunAgentConversation()
{
  // Build configuration to read from user secrets
  var configuration = new ConfigurationBuilder()
      .AddUserSecrets<Program>()
      .Build();

  var endpointUrl = configuration["AzureAI:Endpoint"];
  var agentId = configuration["AzureAI:AgentId"];

  if (string.IsNullOrEmpty(endpointUrl) || string.IsNullOrEmpty(agentId))
  {
    Console.WriteLine("Error: Missing configuration. Please set up user secrets:");
    Console.WriteLine("dotnet user-secrets set \"AzureAI:Endpoint\" \"your-endpoint-url\"");
    Console.WriteLine("dotnet user-secrets set \"AzureAI:AgentId\" \"your-agent-id\"");
    return;
  }

  var endpoint = new Uri(endpointUrl);
  AIProjectClient projectClient = new(endpoint, new DefaultAzureCredential());

  PersistentAgentsClient agentsClient = projectClient.GetPersistentAgentsClient();
  PersistentAgent agent = agentsClient.Administration.GetAgent(agentId);
  
  var response = await agentsClient.Threads.CreateThreadAsync([]);
  var thread = response.Value;

  if (thread == null)
  {
    Console.WriteLine("Failed to create a thread.");
    return;
  }

  // PersistentAgentThread thread = agentsClient.Threads.GetThread("thread_mAjFk4YbatbREKdtC38gFI4Q");

  Console.WriteLine("Chat with BingGroundingAgent01 (type 'exit' to quit):");
  Console.WriteLine("=" + new string('=', 50));

  while (true)
  {        // Get user input
    Console.Write("\nYou: ");
    string? userInput = Console.ReadLine();

    // Check for exit condition
    if (string.IsNullOrWhiteSpace(userInput) || userInput.ToLower() == "exit")
    {
      Console.WriteLine("Goodbye!");
      break;
    }

    // Send user message
    PersistentThreadMessage messageResponse = agentsClient.Messages.CreateMessage(
        thread.Id,
        MessageRole.User,
        userInput);

    // Create and run the agent
    ThreadRun run = agentsClient.Runs.CreateRun(
        thread.Id,
        agent.Id);

    // Poll until the run reaches a terminal status
    Console.Write("Agent is thinking");
    do
    {
      await Task.Delay(TimeSpan.FromMilliseconds(500));
      Console.Write(".");
      run = agentsClient.Runs.GetRun(thread.Id, run.Id);
    }
    while (run.Status == RunStatus.Queued
        || run.Status == RunStatus.InProgress);

    Console.WriteLine(); // New line after dots

    if (run.Status != RunStatus.Completed)
    {
      Console.WriteLine($"Error: Run failed or was canceled: {run.LastError?.Message}");
      continue;
    }        // Get and display all messages and show only the latest non-user message
    Pageable<PersistentThreadMessage> messages = agentsClient.Messages.GetMessages(
        thread.Id, order: ListSortOrder.Descending);

    // Display only the latest response that is not from the user
    var latestMessage = messages.FirstOrDefault(m => m.Role != MessageRole.User);
    if (latestMessage != null)
    {
      Console.Write("Agent: ");
      foreach (MessageContent contentItem in latestMessage.ContentItems)
      {
        if (contentItem is MessageTextContent textItem)
        {
          Console.Write(textItem.Text);
        }
        else if (contentItem is MessageImageFileContent imageFileItem)
        {
          Console.Write($"<image from ID: {imageFileItem.FileId}>");
        }
      }
      Console.WriteLine();
    }
  }
}

// Main execution
await RunAgentConversation();