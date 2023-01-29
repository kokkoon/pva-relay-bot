using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Connector.DirectLine;
using Microsoft.PowerVirtualAgents.Samples.BotConnectorApp;

namespace BotConnectorApi.Controllers;

[ApiController]
[Route("[controller]")]
public class BotConnectorController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private static string _watermark = null;
    private const int _botReplyWaitIntervalInMilSec = 3000;
    private const string _botDisplayName = "Bot";
    private const string _userDisplayName = "You";
    private static string s_endConversationMessage;
    private static BotService s_botService;
    public BotConnectorController(IConfiguration configuration)
    {
        _configuration = configuration;
        var botId = _configuration.GetValue<string>("BotId") ?? string.Empty;
        var tenantId = _configuration.GetValue<string>("BotTenantId") ?? string.Empty;
        var botTokenEndpoint = _configuration.GetValue<string>("BotTokenEndpoint") ?? string.Empty;
        var botName = _configuration.GetValue<string>("BotName") ?? string.Empty;
        s_botService = new BotService()
        {
            BotName = botName,
            BotId = botId,
            TenantId = tenantId,
            TokenEndPoint = botTokenEndpoint,
        };
        //StartConversation1().Wait();
    }

    [HttpGet]
    [Route("GetToken")]
    public async Task<ActionResult> GetToken()
    {
        var token = await s_botService.GetTokenAsync();
        return Ok(token);
    }

    [HttpPost]
    [Route("StartBot")]
    public async Task<ActionResult> StartBot(string inputMessage)
    {
        var response = await StartConversation(inputMessage);
        return Ok(response);
    }

    [HttpPost]
    [Route("StartBotSession")]
    public async Task<ActionResult> StartBotSession(string token, string inputMessage)
    {

        var botId = _configuration.GetValue<string>("BotId") ?? string.Empty;
        var tenantId = _configuration.GetValue<string>("BotTenantId") ?? string.Empty;
        var botTokenEndpoint = _configuration.GetValue<string>("BotTokenEndpoint") ?? string.Empty;
        var botName = _configuration.GetValue<string>("BotName") ?? string.Empty;
        s_endConversationMessage = _configuration.GetValue<string>("EndConversationMessage") ?? "quit";
        if (string.IsNullOrEmpty(botId) || string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(botTokenEndpoint) || string.IsNullOrEmpty(botName))
        {
            Console.WriteLine("Update App.config and start again.");
            Console.WriteLine("Press any key to exit");
            Console.Read();
            Environment.Exit(0);
        }


        var response = await StartConversation(inputMessage, token);
        return Ok(response);
    }

    //private static async Task<string> StartConversation(string inputMsg)
    private static async Task<string> StartConversation(string inputMsg, string token = "")
    {
        var token1 = String.IsNullOrEmpty(token)? await s_botService.GetTokenAsync():token;
        using (var directLineClient = new DirectLineClient(token1))
        {
            var conversation = await directLineClient.Conversations.StartConversationAsync();
            var conversationtId = conversation.ConversationId;
            //string inputMessage;

            if (!string.IsNullOrEmpty(inputMsg) && !string.Equals(inputMsg, s_endConversationMessage))
            {
                // Send user message using directlineClient
                await directLineClient.Conversations.PostActivityAsync(conversationtId, new Activity()
                {
                    Type = ActivityTypes.Message,
                    From = new ChannelAccount { Id = "userId", Name = "userName" },
                    Text = inputMsg,
                    TextFormat = "plain",
                    Locale = "en-Us",
                });

                //Console.WriteLine($"{_botDisplayName}:");
                //Thread.Sleep(_botReplyWaitIntervalInMilSec);

                // Get bot response using directlinClient
                List<Activity> responses = await GetBotResponseActivitiesAsync(directLineClient, conversationtId);
                return BotReplyAsAPIResponse(responses);
            }

            return "Thank you.";
        }
    }

    private static string BotReplyAsAPIResponse(List<Activity> responses)
    {
        string responseStr = "";
        responses?.ForEach(responseActivity =>
        {
            // responseActivity is standard Microsoft.Bot.Connector.DirectLine.Activity
            // See https://github.com/Microsoft/botframework-sdk/blob/master/specs/botframework-activity/botframework-activity.md for reference
            // Showing examples of Text & SuggestedActions in response payload
            if (!string.IsNullOrEmpty(responseActivity.Text))
            {
                responseStr = responseStr + string.Join(Environment.NewLine, responseActivity.Text);
            }

            if (responseActivity.SuggestedActions != null && responseActivity.SuggestedActions.Actions != null)
            {
                var options = responseActivity.SuggestedActions?.Actions?.Select(a => a.Title).ToList();
                responseStr = responseStr + $"\t{string.Join(" | ", options)}";
            }
        });

        return responseStr;
    }

    /// <summary>
    /// Use directlineClient to get bot response
    /// </summary>
    /// <returns>List of DirectLine activities</returns>
    /// <param name="directLineClient">directline client</param>
    /// <param name="conversationtId">current conversation ID</param>
    /// <param name="botName">name of bot to connect to</param>
    private static async Task<List<Activity>> GetBotResponseActivitiesAsync(DirectLineClient directLineClient, string conversationtId)
    {
        ActivitySet response = null;
        List<Activity> result = new List<Activity>();

        do
        {
            response = await directLineClient.Conversations.GetActivitiesAsync(conversationtId, _watermark);
            if (response == null)
            {
                // response can be null if directLineClient token expires
                Console.WriteLine("Conversation expired. Press any key to exit.");
                Console.Read();
                directLineClient.Dispose();
                Environment.Exit(0);
            }

            _watermark = response?.Watermark;
            result = response?.Activities?.Where(x =>
                x.Type == ActivityTypes.Message &&
                string.Equals(x.From.Name, s_botService.BotName, StringComparison.Ordinal)).ToList();

            if (result != null && result.Any())
            {
                return result;
            }

            Thread.Sleep(1000);
        } while (response != null && response.Activities.Any());

        return new List<Activity>();
    }

}