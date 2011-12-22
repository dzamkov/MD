namespace MD.OpenTK

open MD
open MD.UI
open MD.OpenTK
open MD.Reflection
open System
open System.Collections.Generic
open global.OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL
open GLExtensions

/// Cached information about a tile in a tile procedure.
type TileData<'a> = {
    
    /// The tile this data is for.
    Tile : 'a

    /// The texture for this tile, if loaded.
    mutable Texture : Texture exclusive option

    /// The image for this tile, if loaded. Note that the image will be released as
    /// soon as a texture is created for it.
    mutable Image : (Image exclusive * ImageSize) option

    /// A retract action to quit loading the tile data, if it is currently loading.
    mutable RetractLoad : Retract option

    } with

    /// Initializes tile data for the given tile.
    static member Initialize (tile : 'a) = { Tile = tile; Texture = None; Image = None; RetractLoad = None }

    /// Processes this tile data in order to find its texture. If not available, None is
    /// returned.
    member this.Process () =
        match this.Image, this.Texture with
        | Some (image, imageSize), _ ->
            let texture = Texture.Create (!!image, imageSize)
            Texture.CreateMipmap GenerateMipmapTarget.Texture2D
            Texture.SetFilterMode (TextureTarget.Texture2D, TextureMinFilter.LinearMipmapLinear, TextureMagFilter.Linear)
            image.Finish ()
            this.Image <- None
            this.Texture <- Some (texture |> Exclusive.custom (fun texture -> texture.Delete ()))
            Some texture
        | _, Some texture -> Some !!texture
        | _, _ -> None

    /// Gets wether the image data for this tile is currently loading.
    member this.Loading = this.RetractLoad.IsSome

    /// Deletes this tile data.
    member this.Delete () =
        match this.Texture with
        | Some texture -> texture.Finish ()
        | None -> ()
        match this.Image with
        | Some (image, _) -> image.Finish ()
        | None -> ()
        

/// A procedure for rendering a tile image (with a specific tile type).
type TileProcedure<'a when 'a : equality> (image : TileImage<'a>) =
    inherit Procedure ()
    let tileCache = new Dictionary<'a, TileData<'a>> ()
    let mutable deleted = false

    /// Gets wether this tile procedure has been deleted.
    member this.Deleted = deleted

    /// Begins loading the image for the given tile data.
    member this.BeginLoad (tileData : TileData<'a>) =
        let tile = tileData.Tile
        let onLoad (image : Image exclusive, imageSize) =
            if this.Deleted then image.Finish ()
            else 
                tileData.Image <- Some (image, imageSize)
                tileData.RetractLoad <- None
        tileData.RetractLoad <- Some (image.RequestTileImage (tile, onLoad))

    /// Gets the cached tile data for the given tile, creating it if needed.
    member this.GetTileData (tile : 'a) =
        let mutable tileData = Unchecked.defaultof<TileData<'a>>
        if tileCache.TryGetValue (tile, &tileData) then tileData
        else
            let tileData = TileData.Initialize tile
            tileCache.Add (tile, tileData)
            tileData

    override this.Invoke context = 
        let tiles = image.GetTiles (context.ViewBounds &&& image.Area, context.Resolution)
        for tile in tiles do
            let tileData = this.GetTileData tile
            match tileData.Process () with
            | Some texture ->
                context.PushTransform (Transform.Place (image.GetTileArea tile))
                context.RenderTexture texture
                context.Pop ()
            | None -> if not tileData.Loading then this.BeginLoad tileData

    override this.Delete () =
        deleted <- true
        for kvp in tileCache do
            kvp.Value.Delete ()

/// Contains functions for constructing and manipulating tile procedures.
module TileProcedure =

    let genericTileImageType = typeof<TileImage<unit>>.GetGenericTypeDefinition ()
    let genericTileProcedureType = typeof<TileProcedure<unit>>.GetGenericTypeDefinition ()

    /// Creates a tile procedure for the given image.
    let create : TileImage -> Procedure = mapType genericTileImageType genericTileProcedureType