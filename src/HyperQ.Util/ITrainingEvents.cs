using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    public interface ITrainingEvents
    {
        event Action OnEpisodeStart;
        event Action OnEpisodeEnd;
        event Action OnStepStart;
        event Action OnStepEnd;
        event Action OnDynaStart;
        event Action OnDynaEnd;
        event Action OnMemoryReplayStart;
        event Action OnMemoryReplayEnd;
    }
}
