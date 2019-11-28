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
    let x = 1

[<TypeProvider>]
type BasicErasingProvider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces (config, assemblyReplacementMap=[("FSharp.AWS.S3TypeProvider.DesignTime", "FSharp.AWS.S3TypeProvider.Runtime")], addDefaultProbingLocation=true)

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
        let typedBucket = ProvidedTypeDefinition(asm, ns, "S3Bucket", Some typeof<obj>)
        typedBucket.AddXmlDoc(sprintf "A strongly typed interface to S3 bucket %s which was created on %A"                                  bucket.BucketName bucket.CreationDate)

        typedBucket.AddMember(ProvidedProperty("CreationDate", typeof<DateTime>, getterCode = (fun _ -> <@@ bucket.CreationDate @@>), isStatic = true))

        typedBucket

    let createTypes () =
        let typedS3 = ProvidedTypeDefinition(asm, ns, "Profile", Some typeof<obj>)

        let typeParams = [ ProvidedStaticParameter("profile", typeof<string>) ]

        let initFuncation (typeName: string) (parameters: obj[]) =
            match parameters with
            | [|:? string as profile|] ->
                let typedS3Profile = ProvidedTypeDefinition(asm, ns, typeName, Some typeof<obj>)
                typedS3Profile.AddXmlDoc(sprintf "A strongly typed interface to S3 account using .aws/config")
            
                typedS3Profile.AddMembersDelayed(fun() -> getClient profile |> getBuckets |> List.map (createTypedBucket typedS3Profile))

        // let ctor = ProvidedConstructor([], invokeCode = fun args -> <@@ "My internal state" :> obj @@>)
        // typedS3.AddMember(ctor)

        // let ctor2 = ProvidedConstructor([ProvidedParameter("InnerState", typeof<string>)], invokeCode = fun args -> <@@ (%%(args.[0]):string) :> obj @@>)
        // myType.AddMember(ctor2)

        // let innerState = ProvidedProperty("InnerState", typeof<string>, getterCode = fun args -> <@@ (%%(args.[0]) :> obj) :?> string @@>)
        // typedS3.AddMember(innerState)

        // let meth = ProvidedMethod("StaticMethod", [], typeof<DataSource>, isStatic=true, invokeCode = (fun args -> Expr.Value(null, typeof<DataSource>)))
        // typedS3.AddMember(meth)

        // let nameOf =
        //     let param = ProvidedParameter("p", typeof<Expr<int>>)
        //     param.AddCustomAttribute {
        //         new CustomAttributeData() with
        //             member __.Constructor = typeof<ReflectedDefinitionAttribute>.GetConstructor([||])
        //             member __.ConstructorArguments = [||] :> _
        //             member __.NamedArguments = [||] :> _
        //     }
        //     ProvidedMethod("NameOf", [ param ], typeof<string>, isStatic = true, invokeCode = fun args ->
        //         <@@
        //             match (%%args.[0]) : Expr<int> with
        //             | Microsoft.FSharp.Quotations.Patterns.ValueWithName (_, _, n) -> n
        //             | e -> failwithf "Invalid quotation argument (expected ValueWithName): %A" e
        //         @@>)
        // typedS3.AddMember(nameOf)

        [typedS3]

    do
        this.AddNamespace(ns, createTypes())

[<TypeProviderAssembly>]
do ()
