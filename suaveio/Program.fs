﻿open System
open Suave
open Suave.Http
open Suave.Operators
open Suave.Filters
open RealWorld.Stubs
open System.IO
open Microsoft.Extensions.Configuration
open MongoDB.Driver
open RealWorld.Models
open RealWorld.Effects.DB
open MongoDB.Bson

let serverConfig = 
  { defaultConfig with bindings = [HttpBinding.createSimple HTTP "127.0.0.1" 8073] }

let jsonToString (json: 'a) = json |> Suave.Json.toJson |> System.Text.Encoding.UTF8.GetString
let fakeReply email = 
  {User = { Email = email; Token = ""; Username=""; Bio=""; Image=""; PasswordHash=""; }; Id=(BsonObjectId(ObjectId.GenerateNewId()))  }

let validateCredentials dbClient = 
  request (fun inputGraph -> 
    let user = Suave.Json.fromJson<UserRequest> inputGraph.rawForm |> loginUser dbClient
    Successful.OK (sprintf "%A" inputGraph)
  )

let registerUserNewUser dbClient = 
  request ( fun inputGraph -> 
    Suave.Json.fromJson<UserRequest> inputGraph.rawForm
    |> registerWithBson dbClient 
    |> Realworld.Convert.userRequestToUser 
    |> jsonToString 
    |> Successful.OK
  )
    
let getCurrentUser dbClient =
  request (fun inputGraph ->
    Successful.OK (fakeReply "" |> jsonToString)
  )

let updateUser dbClient = 
  request (fun inputGraph ->
    let userToUpdate = (Suave.Json.fromJson<User> inputGraph.rawForm).User

    userToUpdate
    |> updateRequestedUser dbClient
    |> Realworld.Convert.updateUser userToUpdate
    |> Successful.OK
  )
  
//TODO: Replace each return comments with function to carry out the action.
let app (dbClient: IMongoDatabase) = 
  choose [
    POST >=> path "/users/login" >=> validateCredentials dbClient
    POST >=> path "/users" >=> registerUserNewUser dbClient
    GET  >=> path "/user" >=> getCurrentUser dbClient
    PUT  >=> path "/user" >=> updateUser dbClient
    GET  >=> path "/profile/:username" >=> (Successful.OK Responses.singleProfile) 
    POST >=> path "/profiles/:username/follow" >=> (Successful.OK Responses.singleProfile)
    DELETE >=> path "/profiles/:username/follow" >=> (Successful.OK Responses.singleProfile)
    GET  >=> path "/articles" >=> (Successful.OK Responses.multipleArticles)
    GET  >=> path "/articles/feed" >=> (Successful.OK Responses.multipleArticles)
    GET  >=> path "/articles/:slug" >=> (Successful.OK Responses.singleArticle)
    PUT  >=> path "/articles/:slug" >=> (Successful.OK Responses.singleArticle)
    DELETE >=> path "/articles/:slug" >=> (Successful.OK Responses.singleArticle)
    POST >=> path "/articles/:slug/comments" >=> (Successful.OK Responses.singleComment)
    GET  >=> path "/articles/:slug/comments" >=> (Successful.OK Responses.multipleComments)
    DELETE >=> path "/articles/:slug/comments/:id" >=> (Successful.OK Responses.multipleComments)
    POST >=> path "/articles/:slug/favorite" >=> (Successful.OK Responses.singleArticle)
    DELETE >=> path "/articles/:slug/favorite" >=> (Successful.OK Responses.singleArticle)
    POST >=> path "/articles" >=> (Successful.OK Responses.singleArticle) // Creates a new article
    GET >=> path "/tags" >=> (Successful.OK Responses.tagList)
    path "/" >=> (Successful.OK "This will return the base page.")
  ]

open RealWorld.Effects.DB

[<EntryPoint>]
let main argv = 
  startWebServer serverConfig (RealWorld.Effects.DB.getDBClient () |> app)
  0