using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ago.Core.LLM
{
    public interface IChatClient
    {
        Task<ChatResponse> SendAsync(IReadOnlyList<ChatMessage> messages, CancellationToken ct = default);

        /// <summary>
        /// Quick liveness check — used for fallback logic.
        /// </summary>
        Task<bool> IsAvailableAsync(CancellationToken ct = default);
    }
}
