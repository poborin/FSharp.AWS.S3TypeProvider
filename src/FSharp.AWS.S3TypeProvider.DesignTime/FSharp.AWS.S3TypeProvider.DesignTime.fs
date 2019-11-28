module FSharp.AWS.S3TypeProviderImplementation

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open FSharp.Quotations
open FSharp.Core.CompilerServices
open MyNamespace
open ProviderImplementation
open ProviderImplementation.ProvidedTypes
open Amazon.S3
open Amazon.S3.Model

// Put any utility helpers here
[<AutoOpen>]
module internal Helpers =
    let erasedType<'T> assemblyName rootNamespace typeName = 
        ProvidedTypeDefinition(assemblyName, rootNamespace, typeName, Some(typeof<'T>), hideObjectMethods = true)

    let runtimeType<'T> typeName = 
        ProvidedTypeDefinition(typeName, Some typeof<'T>, hideObjectMethods = true)

[<TypeProvider>]
type BasicErasingProvider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces(config) //config, assemblyReplacementMap=[("FSharp.AWS.S3TypeProvider.DesignTime", "FSharp.AWS.S3TypeProvider.Runtime")], addDefaultProbingLocation=true)

    let ns = "FSharp.AWS.S3TypeProvider"
    let asm = Assembly.GetExecutingAssembly()

    // check we contain a copy of runtime files, and are not referencing the runtime DLL
    do assert (typeof<DataSource>.Assembly.GetName().Name = asm.GetName().Name)  

    let getBuckets (client: AmazonS3Client) =
        client.ListBucketsAsync() 
            |> Async.AwaitTask 
            |> Async.RunSynchronously
            |> (fun arg -> arg.Buckets)
            |> List.ofSeq

    let getClient profile =
        Environment.SetEnvironmentVariable("AWS_PROFILE", profile)
        let config = AmazonS3Config()
        new AmazonS3Client(config)

    let createTypedBucket(ownerType: ProvidedTypeDefinition) (bucket: S3Bucket) =
        let typedBucket = runtimeType<obj> bucket.BucketName
        typedBucket.AddXmlDoc(sprintf "A strongly typed interface to S3 bucket %s which was created on %A"                                  bucket.BucketName bucket.CreationDate)

        typedBucket.AddMember(ProvidedProperty("CreationDate", typeof<DateTime>, getterCode = (fun _ -> <@@ bucket.CreationDate @@>), isStatic = true))

        typedBucket

    let createTypes () =
        let typedS3 = erasedType<obj> asm ns "Profile"
        
        //ProvidedTypeDefinition(asm, ns, "Profile", Some typeof<obj>, hideObjectMethods = true)

        let typeParams = [ ProvidedStaticParameter("profile", typeof<string>) ]

        let initFunction (typeName: string) (parameters: obj[]) =
            match parameters with
            | [|:? string as profile|] ->
                let typedS3Profile = erasedType<obj> asm ns typeName
                //ProvidedTypeDefinition(typeName, Some typeof<obj>, hideObjectMethods = true)
                typedS3Profile.AddXmlDoc(sprintf "A strongly typed interface to S3 account using .aws/config")
            
                typedS3Profile.AddMembersDelayed(fun() -> getClient profile |> getBuckets |> List.map (createTypedBucket typedS3Profile))
                typedS3Profile

        typedS3.DefineStaticParameters(parameters = typeParams, instantiationFunction = initFunction)
        [typedS3]

    do
        this.AddNamespace(ns, createTypes())

[<TypeProviderAssembly>]
do ()
