/// > .paket\paket.exe generate-load-scripts --group Main --framework net461 --type fsx
#load @".paket/load/net461/main.group.fsx"

open System
open Akka.Cluster.Tools.Singleton
open Akkling
open Akka.Routing
open Akka.Cluster
open Akka

let configWithPort (port:int) (role:string) =
    let config = Configuration.parse ("""
        akka {
          actor {
            provider = cluster
            deployment {
                /sftp {
                  router = round-robin-pool
                  cluster {
                    enabled = on
                    max-nr-of-instances-per-node = 2
                    max-total-nr-of-instances = 10
                    use-role = "worker"
                  }
               }
            }
          }
          remote {
            dot-netty.tcp {
              public-hostname = "localhost"
              hostname = "localhost"
              port = """ + port.ToString() + """
            }
          }
          cluster {
            roles = [""" + role + """]
            seed-nodes = [ "akka.tcp://cluster-system@localhost:5000" ]
          }
        }
        """)
    config.WithFallback(ClusterSingletonManager.DefaultConfig())

let dispatcher = System.create "cluster-system" (configWithPort 5000 "dispatcher")
let _worker1 = System.create "cluster-system" (configWithPort 5001 "worker")
let _worker2 = System.create "cluster-system" (configWithPort 5002 "worker")
// let worker3 = System.create "cluster-system" (configWithPort 5003 "worker")

/// Domain
type SftpCommand = 
    | Upload of src:string * dest:string 
    | Delete of dest:string

/// Actors
let sftpWorkerActor (mailbox:Actor<_>) (msg:SftpCommand) =
    let nodeAddress = Cluster.Get(mailbox.System).SelfUniqueAddress
    match msg with
    | Upload (src, dest) -> logInfof mailbox "Upload from %s to %s on [%A]" src dest nodeAddress
    | Delete (dest) -> logInfof mailbox "Delete %s on [%A]" dest nodeAddress
    ignored ()

let pool = spawn dispatcher "sftp" { props (actorOf2 sftpWorkerActor) with Router = Some (FromConfig.Instance :> RouterConfig) }

pool <! Upload("C:/foo", "\\a")
pool <! Upload("C:/foo", "\\b")
pool <! Upload("C:/foo", "\\c")
pool <! Delete("\\a")
pool <! Delete("\\b")
pool <! Delete("\\c")