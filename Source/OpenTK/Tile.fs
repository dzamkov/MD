namespace MD.OpenTK

open MD
open MD.UI
open MD.OpenTK
open System
open System.Collections.Generic
open global.OpenTK
open OpenTK.Graphics
open OpenTK.Graphics.OpenGL
open GLExtensions

/// Cached information about a tile for a tile procedure.
type TileData = {
    
    /// The tile this data is for.
    Tile : Tile

    /// The texture for this tile, if one is loaded.
    mutable Texture : Texture exclusive option

    /// The image for this tile, if it is available and has yet to be converted
    /// into a texture.
    mutable Image : Image<Paint> exclusive option

    /// A retract action to cancel the image request for this tile, if one is active.
    mutable RetractRequest : Retract option
    
    /// The tile data for the tiles of the selected division of this tile, if loaded.
    mutable Division : TileData[] option

    } with

    /// Creates an empty tile data record for the given tile.
    static member Create (tile : Tile) = {
            Tile = tile
            Texture = None
            Image = None
            RetractRequest = None
            Division = None
        }

    /// Gets the area this tile occupies.
    member this.Area = this.Tile.Area

    /// Gets the tile data for the tiles in the selected division of this tile. If a division has
    /// not yet been selected, one will be chosen based on the current render resolution. If there are
    /// no available divisions, None is returned.
    member this.GetDivision (resolution : Point) =
        match this.Division with
        | Some division -> division
        | None ->
            let division = Seq.head this.Tile.Divisions
            let division = Array.map TileData.Create division
            this.Division <- Some division
            division

    /// Deletes this tile data and all of its children.
    member this.Delete () =
        match this.Texture with
        | Some texture -> texture.Finish ()
        | None -> ()
        match this.Image with
        | Some image -> image.Finish ()
        | None -> ()
        match this.Division with
        | Some division -> for tile in division do tile.Delete ()
        | None -> ()
        this.Texture <- None
        this.Image <- None
        this.Division <- None

    /// Processes this tile and returns true if it is ready to render.
    member this.Process isRoot = 

        // If there is image data, load it as a texture.
        match this.Image with
        | Some image ->

            // Release current texture.
            match this.Texture with
            | Some texture -> texture.Finish ()
            | None -> ()

            // Create new texture.
            let texture = Texture.Create !!image
            if isRoot then

                // Only generate mipmaps for the root tile.
                Texture.CreateMipmap GenerateMipmapTarget.Texture2D
                Texture.SetFilterMode (TextureTarget.Texture2D, TextureMinFilter.LinearMipmapLinear, TextureMagFilter.Nearest)
            else
                Texture.SetFilterMode (TextureTarget.Texture2D, TextureMinFilter.Linear, TextureMagFilter.Nearest)
            this.Texture <- Some (texture |> Exclusive.custom (fun texture -> texture.Delete ()))

            // Free image.
            image.Finish ()
            this.Image <- None
            true

        | None -> this.Texture.IsSome

    /// Tries rendering this tile with the given context.
    member this.Render (context : Context) = 
        match this.Texture with
        | Some texture -> 
            context.PushTransform (Transform.Place this.Area)
            context.RenderTexture texture.Object
            context.Pop ()
        | None -> ()

/// A procedure for rendering a tiled image.
type TileProcedure (tile : Tile) =
    inherit Procedure ()
    let mutable deleted = false
    let root = TileData.Create tile : TileData

    /// Indicates wether this procedure has been deleted.
    member this.Deleted = deleted

    /// Handles the loading of an image into tile data.
    member this.Load (tile : TileData) (image : Image<Paint> exclusive) =
        if deleted then image.Finish ()
        else 
            tile.Image <- Some image
            tile.RetractRequest <- None

    /// Begins loading image data for the given tile.
    member this.BeginLoad (tile : TileData) suggestedSize =
        tile.RetractRequest <- Some (tile.Tile.RequestImage (suggestedSize, this.Load tile))

    override this.Invoke context =
        let resolution = context.Resolution
        let toRender = new Stack<TileData> ()

        // Updates the given tile and its subtiles and prepares them for rendering. Returns true if the tile will
        // be completely opaque when rendered.
        let rec update isRoot (tile : TileData) =
            let area = tile.Area
            if context.IsVisible area then
                let tilePixelScale = Point.Scale (area.Size, resolution)
                if tilePixelScale.Length > 900.0 then 
                    let mutable shouldRender = false
                    for subTile in tile.GetDivision resolution do
                        let opaque = update false subTile
                        shouldRender <- shouldRender || not opaque
                    if shouldRender then
                        if tile.Process isRoot then
                            toRender.Push tile
                            true
                        else
                            if tile.RetractRequest.IsNone then this.BeginLoad tile (256, 256)
                            false
                    else false
                else false
            else false

        update true root |> ignore
        for tile in toRender do
            tile.Render context

        

    override this.Delete () = 
        deleted <- true
        root.Delete ()