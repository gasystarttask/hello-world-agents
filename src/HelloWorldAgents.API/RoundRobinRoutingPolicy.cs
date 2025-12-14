using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace HelloWorldAgents.API
{
    public class RoundRobinRoutingPolicy(IReadOnlyList<AIAgent> agents) : Microsoft.Agents.AI.Workflows.GroupChatManager
    {
        protected override ValueTask<AIAgent> SelectNextAgentAsync(IReadOnlyList<ChatMessage> history, CancellationToken cancellationToken = default)
        {
            if (agents == null || agents.Count == 0)
            {
                throw new InvalidOperationException("No agents available in the group chat.");
            }

            // Find the last agent that spoke by checking the history
            AIAgent? lastAgent = null;
            foreach(var message in history)
            {
                if (message.Role == ChatRole.Assistant)
                {
                    // Find which agent authored this message
                    lastAgent = agents.FirstOrDefault(a => a.Name == message.AuthorName);
                    if (lastAgent != null)
                    {
                        break;
                    }
                }
            }

            // If no agent has spoken yet, start with the first agent
            if (lastAgent == null)
            {
                return new ValueTask<AIAgent>(agents[0]);
            }

            // Find the index of the last agent and select the next one in round-robin order
            var (index, item) = agents.Index().FirstOrDefault(agent => agent.Item.Name == lastAgent.Name);
            int nextIndex = (index + 1) % agents.Count;
            AIAgent selectedAgent = agents[nextIndex];

            return new ValueTask<AIAgent>(selectedAgent);
        }
    }
}
