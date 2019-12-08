namespace FSharp.AWS.S3TypeProvider

open ProviderImplementation
open ProviderImplementation.ProvidedTypes
open FSharp.Quotations
open FSharp.Core.CompilerServices
open System
open System.Reflection

[<TypeProvider>]
type StressErasingProvider (config : TypeProviderConfig) as this =
    inherit TypeProviderForNamespaces (config, addDefaultProbingLocation=true)


    let ns = "FSharp.AWS.S3TypeProvider"
    let asm = Assembly.GetExecutingAssembly()

    let newProperty t name getter isStatic = ProvidedProperty(name, t, getter, isStatic = isStatic)
    let newStaticProperty t name getter = newProperty t name (fun _ -> getter) true
    let addStaticProperty t name getter (typ:ProvidedTypeDefinition) = typ.AddMember (newStaticProperty t name getter); typ
    let tags = ProvidedTypeDefinition(asm, ns, "Tags", Some typeof<obj>, hideObjectMethods = true)  

    // An example provider with one _optional_ static parameter
    let provider2 = ProvidedTypeDefinition(asm, ns, "Provider2", Some typeof<obj>, hideObjectMethods = true)
    do provider2.DefineStaticParameters([ProvidedStaticParameter("Host", typeof<string>, "default")], fun name args ->
        let provided = 
            let srv = args.[0] :?> string
            let prop = ProvidedProperty("Test", typeof<String>, (fun _ -> <@@ "test" @@>), isStatic = true)
            let provided = ProvidedTypeDefinition(asm, ns, name, Some typeof<obj>, hideObjectMethods = true)
            provided.AddMember prop
            addStaticProperty tags "Tags" <@@ obj() @@> provided |> ignore
            provided

        provided
    )

    do this.AddNamespace(ns, [provider2; tags])

[<assembly:CompilerServices.TypeProviderAssembly()>]
do ()