/// > .paket\paket.exe generate-load-scripts --group Main --framework net461 --type fsx
#load @".paket/load/net461/main.group.fsx"
open Akkling.Cluster.Sharding
open Akka.Actor
open Akka.Cluster
open Akka.Cluster.Sharding
open System
open Akkling

let configWithPort (port:int) =
    let config = Configuration.parse ("""
        akka {
          actor {
            provider = cluster
          }
          remote {
            dot-netty.tcp {
              public-hostname = "localhost"
              hostname = "localhost"
              port = """ + port.ToString() + """
            }
          }
          cluster {
            roles = ["Worker"]
            sharding {
               journal-plugin-id = "akka.persistence.journal.inmem"
               snapshot-plugin-id = "akka.persistence.snapshot-store.local"
            }
            seed-nodes = [ "akka.tcp://cluster-system@localhost:5000" ]
          }
        }
        """)
    config
      .WithFallback(ClusterSharding.DefaultConfig())

let system1 = ActorSystem.Create("cluster-system", configWithPort 5000)
let system2 = ActorSystem.Create("cluster-system", configWithPort 5001)

/// Domain
type FileCommand = {
    ProgramId : string
    Duration : TimeSpan
    FilePath : string
}

/// Actors
let aggregateRootActor (mailbox:Actor<_>) (msg:FileCommand) =
    let nodeAddress = Cluster.Get(mailbox.System).SelfUniqueAddress
    logInfof mailbox "Program: [%s] with path [%s] on [%A]" msg.ProgramId msg.FilePath nodeAddress
    ignored ()

let extractorFunction (message:FileCommand) =
    let entityId = message.ProgramId
    let hash = entityId.GetHashCode()
    let numberOfShards = 4
    let shardId = sprintf "shard_%d" ((abs hash) % numberOfShards)
    shardId, entityId, message

let region1 = spawnSharded extractorFunction system1 "fileRouter" (props (actorOf2 aggregateRootActor))
let region2 = spawnSharded extractorFunction system2 "fileRouter" (props (actorOf2 aggregateRootActor))

region1 <! { ProgramId = "a"; Duration = TimeSpan.FromMinutes 10.; FilePath = "\\a_0.mp4" }
region2 <! { ProgramId = "a"; Duration = TimeSpan.FromMinutes 10.; FilePath = "\\a_1.mp4" }


region1 <! { ProgramId = "b"; Duration = TimeSpan.FromMinutes 8.; FilePath = "\\a_2.mp4" }
region2 <! { ProgramId = "c"; Duration = TimeSpan.FromMinutes 8.; FilePath = "\\a_2.mp4" }
region1 <! { ProgramId = "d"; Duration = TimeSpan.FromMinutes 8.; FilePath = "\\a_2.mp4" }

CoordinatedShutdown.Get(system2).Run().Wait()

region1 <! { ProgramId = "a"; Duration = TimeSpan.FromMinutes 10.; FilePath = "\\a_1.mp4" }
region1 <! { ProgramId = "b"; Duration = TimeSpan.FromMinutes 8.; FilePath = "\\a_2.mp4" }
region1 <! { ProgramId = "c"; Duration = TimeSpan.FromMinutes 8.; FilePath = "\\a_2.mp4" }
region1 <! { ProgramId = "d"; Duration = TimeSpan.FromMinutes 8.; FilePath = "\\a_2.mp4" }
