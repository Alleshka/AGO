using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ago.Core
{
    public class PromptResolver
    {
        // Priority: personal → team → built-in default
        public string Resolve(
            string agentId, 
            string projectRoot)
        {
            // Personal prompt in .ago/prompts/
            var personal = Path.Combine(projectRoot, ".ago", "prompts", $"{agentId}.md");
            if (File.Exists(personal))
            {
                return File.ReadAllText(personal);
            }

            // Team prompt in .ago-prompts/
            var team = Path.Combine(projectRoot, ".ago-prompts", $"{agentId}.md");
            if (File.Exists(team))
            {
                return File.ReadAllText(team);
            }

            // TODO: Built-in default from code
            return null; 
        }
    }
}
