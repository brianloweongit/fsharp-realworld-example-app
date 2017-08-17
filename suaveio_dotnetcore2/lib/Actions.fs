namespace RealWorld.Effects

module Actions = 
  open Suave
  open RealWorld.Models
  open DB
  open MongoDB.Bson
  open System.Text
  open System
  open RealWorld
  open Suave.RequestErrors

  let jsonToString (json: 'a) = 
    Newtonsoft.Json.JsonConvert.SerializeObject(json)

  let fakeReply email = 
    {user = { email = email; token = ""; username=""; bio=""; image=""; passwordhash=""; favorites=[||] }; Id=ObjectId.GenerateNewId().ToString()  }

  let extractStringQueryVal (queryParameters : HttpRequest) name =
    match queryParameters.queryParam name with
    | Choice1Of2 queryVal -> queryVal
    | Choice2Of2 _ -> String.Empty

  let extractNumericQueryVal (queryParameters : HttpRequest) name = 
    match queryParameters.queryParam name with
    | Choice1Of2 limit -> Convert.ToInt32 limit
    | Choice2Of2 _ -> 0
    
  let routeByOptions (queryParameters : HttpRequest) =
    let listArticleOptions = {
      Limit = extractNumericQueryVal queryParameters "limit";
      Tag = extractStringQueryVal queryParameters "tag";
      Author = extractStringQueryVal queryParameters "author";
      Favorited = extractStringQueryVal queryParameters "favorited";
      Offset = extractNumericQueryVal queryParameters "offset";
    }
    (Successful.OK "")
    
  let hashPassword (request: UserRequest) = 
    {request with user = { request.user with hash = RealWorld.Hash.Crypto.fastHash request.user.password } }

  let registerNewUser dbClient = 
    request ( fun inputGraph -> 
      Newtonsoft.Json.JsonConvert.DeserializeObject<UserRequest>(inputGraph.rawForm |> System.Text.ASCIIEncoding.UTF8.GetString)
      |> hashPassword
      |> registerWithBson dbClient 
      |> RealWorld.Convert.userRequestToUser 
      |> jsonToString 
      |> Successful.OK
    )

  let getCurrentUser dbClient httpContext = 
    Auth.useToken httpContext (fun token -> async {
      // try
        
        
        let currentUser = 
          (getUser dbClient token.UserName).Value
          |> BsonDocConverter.toUser
          |> jsonToString

        return! Successful.OK currentUser httpContext
      // with ex ->
      //   return! Suave.RequestErrors.NOT_FOUND "Database not available" httpContext
    })

  let updateUser dbClient = 
    request (fun inputGraph ->
      let userToUpdate = (Suave.Json.fromJson<User> inputGraph.rawForm).user

      userToUpdate
      |> updateRequestedUser dbClient
      |> RealWorld.Convert.updateUser userToUpdate
      |> Successful.OK
    )

  open RealWorld.Stubs
  let getUserProfile dbClient username = 
    (Successful.OK Responses.singleProfile)

  let createNewArticle (articleToAdd : Article) dbCLient = 
    // TODO: add success response
    let succesful = insertNewArticle articleToAdd dbCLient
    articleToAdd 

  let getArticlesBy slug dbClient =
    (* TODO: Add suave testing for this. *)
    getArticleBySlug dbClient slug
    |> RealWorld.Convert.extractArticleList
    |> jsonToString
    |> Successful.OK

  let defaultTagsIfEmpty = function
    | Some tags -> tags
    | None -> { tags = [||] }

  let defaultArticleIfEmpty = function
    | Some articles -> Array.ofList articles
    | None -> [||]

  let getTagList dbClient =
    getSavedTagList dbClient 
    |> defaultTagsIfEmpty
    |> jsonToString
    |> Successful.OK 

  (* TODO: Look into consolidating these functions since they are close in functionality *)
  let getArticles dbClient = 
    getSavedArticles dbClient
    |> RealWorld.BsonDocConverter.toArticleList
    |> jsonToString
    |> Successful.OK

  let getArticlesForFeed dbClient = 
    getSavedFollowedArticles dbClient
    |> defaultArticleIfEmpty
    |> jsonToString
    |> Successful.OK

  let addArticleWithSlug json (slug: string) (dbClient: MongoDB.Driver.IMongoDatabase) = 
    let currentArticle = json |> Suave.Json.fromJson<Article> 
    let updatedSlug = { currentArticle.article with slug = slug}
    
    insertNewArticle ({currentArticle with article = updatedSlug }) dbClient
    |> jsonToString
    |> Successful.OK

  let deleteArticleBy slug dbClient = Successful.OK ((deleteArticleBySlug slug dbClient).ToString())

  let addCommentBy json slug dbClient = 
    let possibleArticleId = getArticleBySlug dbClient slug
    match possibleArticleId with
    | Some articleId -> 
      saveNewComment (Suave.Json.fromJson<Comment> json) (articleId.Id.ToString()) dbClient |> ignore
      Successful.OK (json |> jsonToString)
    | None -> 
      Successful.OK ({errors = {body = [|"Could not find article by slug"|]}} |> jsonToString) 

  let getCommentsBySlug slug dbClient = 
    getCommentsFromArticlesBySlug slug dbClient
    |> jsonToString
    |> Successful.OK 

  let deleteComment (_, (id: string)) dbCLient = 
    deleteWithCommentId id dbCLient
    |> jsonToString
    |> Successful.OK

  let favoriteArticle slug dbClient = 
    // TODO: Get the current user, then get the article by the slug and add the object id to the users favorite list
    Successful.OK ""

  let removeFavoriteCurrentUser slug dbClient = 
    // TODO: Do the same thing as above except remove them from the favorite list
    Successful.OK ""