/// > .paket\paket.exe generate-load-scripts --group Main --framework net461 --type fsx
#load @".paket/load/net461/main.group.fsx"
open Akka.Cluster.Tools.Singleton
open Akka.Actor
open Akka.Cluster
open Akka.Cluster.Tools.PublishSubscribe
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
            seed-nodes = [ "akka.tcp://cluster-system@localhost:5000" ]
          }
          extensions = ["Akka.Cluster.Tools.PublishSubscribe.DistributedPubSubExtensionProvider,Akka.Cluster.Tools"]
        }
        """)
    config
      .WithFallback(ClusterSingletonManager.DefaultConfig())

let system1 = System.create "cluster-system" (configWithPort 5000)
let system2 = System.create "cluster-system" (configWithPort 5001)

/// Domain
type Event = { 
    ProgramId : string
    Status : bool 
}

let topic = "Events"

/// Actors
let listenerActor (mailbox:Actor<_>) =
    let mdr = DistributedPubSub.Get(mailbox.System).Mediator
    mdr.Tell(Subscribe(topic, mailbox.UntypedContext.Self))
    let rec loop () = actor {
        let! (msg:obj) = mailbox.Receive()
        match msg with 
        | :? Akka.Cluster.Tools.PublishSubscribe.SubscribeAck -> 
            logInfo mailbox "Subscribed"
        | :? Akka.Cluster.Tools.PublishSubscribe.UnsubscribeAck -> 
            logInfo mailbox "Unsubscribed"
        | :? Event as result ->
            logInfof mailbox "Result received %A" result
        | _ -> ()
        return! loop ()
    }
    loop ()

spawnAnonymous system1 (props listenerActor) |> ignore
spawnAnonymous system2 (props listenerActor) |> ignore

let mediator1 = DistributedPubSub.Get(system1).Mediator
mediator1.Tell(Publish(topic, { ProgramId = "A"; Status = true }))

let mediator2 = DistributedPubSub.Get(system1).Mediator
mediator2.Tell(Publish(topic, { ProgramId = "B"; Status = false }))