using System;
namespace InputAtlas {
public sealed class RecoveryPolicy {
 DateTime last;public DateTime GraceUntil {get;private set;}
 public RecoveryPolicy(DateTime started){last=started;GraceUntil=started.AddSeconds(45);}
 public bool Observe(DateTime now){bool resumed=(now-last).TotalSeconds>10;if(resumed)GraceUntil=now.AddSeconds(45);last=now;return resumed;}
 public bool ShouldRestart(DateTime now,DateTime heartbeat,DateTime created){DateTime latest=heartbeat>created?heartbeat:created;return now>=GraceUntil&&(now-latest).TotalSeconds>120;}
}
}
