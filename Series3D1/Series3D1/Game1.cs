using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;

namespace XNAseries4
{
    /// <summary>
    /// structure to hold position, color, and normal of a vertex
    /// </summary>
    public struct VertexPositionNormalColored : IVertexType
    {
        public Vector3 Position;
        public Color Color;
        public Vector3 Normal;

        public static int SizeInBytes = 7 * 4;
        public readonly static VertexDeclaration VertexDeclaration = new VertexDeclaration
            (
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(sizeof(float) * 3, VertexElementFormat.Color, VertexElementUsage.Color, 0),
            new VertexElement(sizeof(float) * 4, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0)
            );

        VertexDeclaration IVertexType.VertexDeclaration
        {
            get { return VertexPositionNormalColored.VertexDeclaration; }
        }
    }

    /// <summary>
    /// structure to store 4 weights for every vertex, next to the position, normal and texture coordinates
    /// </summary>
    /// To get a smooth transition between the 4 textures, we assign each vertex 4 weights, one for each texture;
    /// For example: the highest vertex of the terrain would have weight 1 for the snow texture, and height 0 for the other 3 textures,
    /// so that vertex would get its color entirely from the snow texture. A vertex in the middle between the snowy and the rocky region
    /// will have weight 0.5 for both the snow and rock texture and weight 0 for the other 2 textures, which would result in a color taken
    /// for 50% from the snow texture and 50% from the rock texture.
    public struct VertexMultitextured : IVertexType
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector4 TextureCoordinate;
        public Vector4 TexWeights;

        public static int SizeInBytes = (3 + 3 + 4 + 4) * sizeof(float);
        public readonly static VertexDeclaration VertexDeclaration = new VertexDeclaration
        (
            // pass a semantic with each entry
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(sizeof(float) * 3, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            new VertexElement(sizeof(float) * 6, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0),
            // Because there's no standard semantic called textureweights, we'll pass it also as an additional TextureCoordinate.
            // Because we're already passing another TextureCoordinate, we have to give this the index 1, instead of 0.
            // This is the last argument of each entry, and enables us to pass in more than one of each semantic.
            new VertexElement(sizeof(float) * 10, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1)
         );

        VertexDeclaration IVertexType.VertexDeclaration
        {
            get { return VertexMultitextured.VertexDeclaration; }
        }
    }

    /// <summary>
    /// This is the main type for the game
    /// </summary>
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager graphics;
        GraphicsDevice device;
        Effect effect;

        // 3D terrain support
        int terrainWidth;
        int terrainLength;
        float[,] heightData;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        #region positioning with keyboard and mouse support
        // how fast we want our camera to respond to mouse and keyboard input
        const float rotationSpeed = 0.3f;
        const float moveSpeed = 30.0f;
        MouseState originalMouseState; // at the end of every frame, reposition the mouse cursor
        // to the middle of the screen. By comparing the current position of the mouse to the
        // middle position of the screen, we can check every frame how much the mouse has moved

        // Light switches
        bool lightningEnabled = true;
        bool ambientLightningEnabled = true;

        /// <summary>
        /// makes camera react to user input
        /// camera goes forward/backward or strafes left/right when arrow buttons on the keyboard are pressed
        /// camera rotation is directed by mouse input
        /// </summary>
        /// <param name="amount">the amount of time that has passed since the last call, so the camera will turn at a speed not dependant on the computer speed</param>
        private void ProcessInput(float amount)
        {
            // retrieve the current MouseState
            MouseState currentMouseState = Mouse.GetState();
            // check whether this state differs from the original mouse position,
            // in the middle of the screen
            if (currentMouseState != originalMouseState)
            {
                // if there is a difference, the corresponding rotation values are updated
                // according to the amount of mouse movement, multiplied by the time elapsed
                // since the last frame.
                float xDifference = currentMouseState.X - originalMouseState.X;
                float yDifference = currentMouseState.Y - originalMouseState.Y;
                leftrightRot -= rotationSpeed * xDifference * amount;
                updownRot -= rotationSpeed * yDifference * amount;
                Mouse.SetPosition(device.Viewport.Width / 2, device.Viewport.Height / 2);
                UpdateViewMatrix();
            }
            // read out the keyboard, and fill a moveVector accordingly
            Vector3 moveVector = new Vector3(0, 0, 0);
            KeyboardState keyState = Keyboard.GetState();
            if (keyState.IsKeyDown(Keys.Up) || keyState.IsKeyDown(Keys.W))
                moveVector += new Vector3(0, 0, -1);
            if (keyState.IsKeyDown(Keys.Down) || keyState.IsKeyDown(Keys.S))
                moveVector += new Vector3(0, 0, 1);
            if (keyState.IsKeyDown(Keys.Right) || keyState.IsKeyDown(Keys.D))
                moveVector += new Vector3(1, 0, 0);
            if (keyState.IsKeyDown(Keys.Left) || keyState.IsKeyDown(Keys.A))
                moveVector += new Vector3(-1, 0, 0);
            if (keyState.IsKeyDown(Keys.Q))
                moveVector += new Vector3(0, 1, 0);
            if (keyState.IsKeyDown(Keys.Z))
                moveVector += new Vector3(0, -1, 0);
            // listen to light switches
            if (keyState.IsKeyDown(Keys.LeftControl))
                lightningEnabled = false;
            if (keyState.IsKeyUp(Keys.LeftControl))
                lightningEnabled = true;
            if (keyState.IsKeyDown(Keys.LeftShift))
                ambientLightningEnabled = false;
            if (keyState.IsKeyUp(Keys.LeftShift))
                ambientLightningEnabled = true;
            // moveVector is multiplied by the amount variable, which indicates the amount of time passed since the last call
            AddToCameraPosition(moveVector * amount);
        }

        private void AddToCameraPosition(Vector3 vectorToAdd)
        {
            // create the rotation matrix of the camera
            Matrix cameraRotation = Matrix.CreateRotationX(updownRot) * Matrix.CreateRotationY(leftrightRot);
            // use the matrix to transform the moveDirection. This is needed, because if you want
            // the camera to move into the Forward direction, you don’t want it to move into the
            // (0,0,-1) direction, but into the direction that is actually Forward for the camera.
            Vector3 rotatedVector = Vector3.Transform(vectorToAdd, cameraRotation);
            // transform this vector by the rotation of our camera, so ‘Forward’ is actually ‘Forward’ relative to our camera
            cameraPosition += moveSpeed * rotatedVector;
            UpdateViewMatrix();
        }
        #endregion

        #region buffering support
        VertexBuffer terrainVertexBuffer;
        IndexBuffer terrainIndexBuffer;

        // camera position support
        Matrix viewMatrix;
        Matrix projectionMatrix;

        // store the position of the camera
        Vector3 cameraPosition = new Vector3(130, 30, -50);
        // store the rotation values around the side and up vector
        float leftrightRot = MathHelper.PiOver2;
        float updownRot = -MathHelper.Pi / 10.0f;

        /// <summary>
        /// Create a view matrix based on the current camera position and rotation values
        /// </summary>
        /// to create the view matrix, we need the camera position, a point where the camera
        /// looks at, and the vector that’s considered to be ‘up’ by the camera;
        /// the last two vectors depend on the rotation of the camera, so we first need
        /// to construct the camera rotation matrix based on the current rotation values
        private void UpdateViewMatrix()
        {
            // create the camera rotation matrix, by combining the rotation around the X axis (looking up-down) by the rotation around the Y axis (looking left-right)
            Matrix cameraRotation = Matrix.CreateRotationX(updownRot) * Matrix.CreateRotationY(leftrightRot);

            // As target point for our camera, we take the position of our camera, plus the (0,0,-1)
            // ‘forward’ vector. We need to transform this forward vector with the rotation of the
            // camera, so it becomes the forward vector of the camera
            Vector3 cameraOriginalTarget = new Vector3(0, 0, -1);
            Vector3 cameraRotatedTarget = Vector3.Transform(cameraOriginalTarget, cameraRotation);
            Vector3 cameraFinalTarget = cameraPosition + cameraRotatedTarget;

            // We find the ‘Up’ vector the same way: by transforming it with the camera rotation
            Vector3 cameraOriginalUpVector = new Vector3(0, 1, 0);
            Vector3 cameraRotatedUpVector = Vector3.Transform(cameraOriginalUpVector, cameraRotation);

            // Update a view matrix based on the current camera position and rotation values
            viewMatrix = Matrix.CreateLookAt(cameraPosition, cameraFinalTarget, cameraRotatedUpVector);

            // for water reflections, reposition camera to below water, as we would like to see the scene as seen by the water
            Vector3 reflCameraPosition = cameraPosition;
            reflCameraPosition.Y = -cameraPosition.Y + waterHeight * 2;
            Vector3 reflTargetPos = cameraFinalTarget;
            reflTargetPos.Y = -cameraFinalTarget.Y + waterHeight * 2;
            Vector3 cameraRight = Vector3.Transform(new Vector3(1, 0, 0), cameraRotation);
            Vector3 invUpVector = Vector3.Cross(cameraRight, reflTargetPos - reflCameraPosition);
            reflectionViewMatrix = Matrix.CreateLookAt(reflCameraPosition, reflTargetPos, invUpVector);
        }
        #endregion

        #region texturing support

        Texture2D grassTexture;
        Texture2D sandTexture;
        Texture2D rockTexture;
        Texture2D snowTexture;

        /// <summary>
        /// load all textures
        /// </summary>
        private void LoadTextures()
        {
            grassTexture = Content.Load<Texture2D>("grass");
            sandTexture = Content.Load<Texture2D>("sand");
            rockTexture = Content.Load<Texture2D>("rock");
            snowTexture = Content.Load<Texture2D>("snow");
            cloudMap = Content.Load<Texture2D>("cloudMap");
            waterBumpMap = Content.Load<Texture2D>("waterbump");
        }

        private void DrawTerrain(Matrix currentViewMatrix)
        {
            // pass vertices and indices to the MultiTextured technique,
            // which is defined in the Series4Effects.fx file
            effect.CurrentTechnique = effect.Techniques["MultiTextured"];
            // pass in textures
            effect.Parameters["xTexture0"].SetValue(sandTexture);
            effect.Parameters["xTexture1"].SetValue(grassTexture);
            effect.Parameters["xTexture2"].SetValue(rockTexture);
            effect.Parameters["xTexture3"].SetValue(snowTexture);

            Matrix worldMatrix = Matrix.Identity;
            effect.Parameters["xWorld"].SetValue(worldMatrix);
            effect.Parameters["xView"].SetValue(currentViewMatrix);
            effect.Parameters["xProjection"].SetValue(projectionMatrix);

            effect.Parameters["xEnableLighting"].SetValue(lightningEnabled);
            if(ambientLightningEnabled)
                effect.Parameters["xAmbient"].SetValue(0.4f); // turn on ambient light
            else
                effect.Parameters["xAmbient"].SetValue(0f); // turn off ambient light
            effect.Parameters["xLightDirection"].SetValue(new Vector3(-0.5f, -1, -0.5f)); // turn on the light

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();

                device.SetVertexBuffer(terrainVertexBuffer);
                device.Indices = terrainIndexBuffer;

                int noVertices = terrainVertexBuffer.VertexCount;
                int noTriangles = terrainIndexBuffer.IndexCount / 3;
                device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, noVertices, 0, noTriangles);
            }
        }
        #endregion

        #region skydome support
        Texture2D cloudMap;
        Model skyDome;
        private void DrawSkyDome(Matrix currentViewMatrix)
        {
            GraphicsDevice.DepthStencilState = DepthStencilState.None;

            Matrix[] modelTransforms = new Matrix[skyDome.Bones.Count];
            skyDome.CopyAbsoluteBoneTransformsTo(modelTransforms);

            // reposition the skydome always around the camera, and disable Z buffer writing when rendering the skybox
            // scale up the dome by factor 100 and position it over the camera, and move it slightly downward, so it’s edges are below the camera
            Matrix wMatrix = Matrix.CreateTranslation(0, -0.3f, 0) * Matrix.CreateScale(100) * Matrix.CreateTranslation(cameraPosition);
            foreach (ModelMesh mesh in skyDome.Meshes)
            {
                foreach (Effect currentEffect in mesh.Effects)
                {
                    Matrix worldMatrix = modelTransforms[mesh.ParentBone.Index] * wMatrix;
                    // pass the cloudMap texture and render the dome using the SkyDome technique
                    currentEffect.CurrentTechnique = currentEffect.Techniques["Textured"];
                    currentEffect.Parameters["xWorld"].SetValue(worldMatrix);
                    currentEffect.Parameters["xView"].SetValue(currentViewMatrix);
                    currentEffect.Parameters["xProjection"].SetValue(projectionMatrix);
                    currentEffect.Parameters["xTexture"].SetValue(cloudMap);
                    currentEffect.Parameters["xEnableLighting"].SetValue(false);
                }
                mesh.Draw();
            }
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }
        #endregion

        #region water refraction support
        // render the scene, as we see it through the camera, into a texture.
        // in cases where some hills obstruct the view to the bottom of the river behind them,
        // for the pixels of that river, sample the color of the hill, instead of the bottom of the river
        const float waterHeight = 5.0f;
        RenderTarget2D refractionRenderTarget;
        Texture2D refractionMap;

        /// <summary>
        /// draw the things that are below the water, and clip everything above the water away
        /// </summary>
        /// <param name="height">height level of the water</param>
        /// <param name="planeNormalDirection">the normal direction of a clip plane</param>
        /// <param name="currentViewMatrix">current view matrix</param>
        /// <param name="clipSide">clip below (true) or above (false) the plane?</param>
        /// <returns>clipping plane</returns>
        private Plane CreatePlane(float height, Vector3 planeNormalDirection, Matrix currentViewMatrix, bool clipSide)
        {
            // make sure our normal is of unity length
            planeNormalDirection.Normalize();
            // create the plane coefficients, from which XNA can create a Plane
            Vector4 planeCoeffs = new Vector4(planeNormalDirection, height);
            if (clipSide)
                planeCoeffs *= -1;

            // as the clipping will occur in hardware after they have passed the vertex shader,
            // the vertices which will be compared to the plane will already be in camera space coordinates;
            // this means we need to transform our plane coefficients with the inverse of the camera matrix,
            // before creating the plane from the coefficients
            Matrix worldViewProjection = currentViewMatrix * projectionMatrix;
            Matrix inverseWorldViewProjection = Matrix.Invert(worldViewProjection);
            inverseWorldViewProjection = Matrix.Transpose(inverseWorldViewProjection);
            // transform our plane coefficients into the correct space, and create the plane
            //planeCoeffs = Vector4.Transform(planeCoeffs, inverseWorldViewProjection);

            Plane finalPlane = new Plane(planeCoeffs);
            return finalPlane;
        }

        /// <summary>
        /// draw the refraction map
        /// </summary>
        private void DrawRefractionMap()
        {
            // create a horizontal plane, a bit above water surface
            Plane refractionPlane = CreatePlane(waterHeight + 1.5f, new Vector3(0, -1, 0), viewMatrix, false);
            effect.Parameters["ClipPlane0"].SetValue(new Vector4(refractionPlane.Normal, refractionPlane.D));
            // enable clipping to create a refraction map
            effect.Parameters["Clipping"].SetValue(true);
            // set the custom render target as current render target and clean it
            device.SetRenderTarget(refractionRenderTarget);
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Black, 1.0f, 0);
            // render the terrain onto the render target (only the part below the clip plane will be rendered)
            DrawTerrain(viewMatrix);
            // turn clipping back off so the whole scene doesn't keep rendering as clipped
            effect.Parameters["Clipping"].SetValue(false);
            device.SetRenderTarget(null);
            // store the contents of the render target into the refractionMap texture
            refractionMap = refractionRenderTarget;
            // dump the texture to a file
            //DumpToFile(refractionMap);
        }
        #endregion

        #region water reflection support
        // reposition camera under water (Y-coordinate mirrored), as we would like to see the scene as seen by the water
        // if part of the terrain under the water obstructs the ray of the camera below the water, clip away all parts of the terrain that are below the water
        RenderTarget2D reflectionRenderTarget;
        Texture2D reflectionMap;
        Matrix reflectionViewMatrix;

        /// <summary>
        /// Draw the reflection map
        /// </summary>
        private void DrawReflectionMap()
        {
            // create a horizontal plane, a bit below water surface
            Plane reflectionPlane = CreatePlane(waterHeight - 0.5f, new Vector3(0, -1, 0), reflectionViewMatrix, true);
            effect.Parameters["ClipPlane0"].SetValue(new Vector4(reflectionPlane.Normal, reflectionPlane.D));
            // enable clipping to create a refraction map
            effect.Parameters["Clipping"].SetValue(true);
            // set the custom render target as current render target and clean it
            device.SetRenderTarget(reflectionRenderTarget);
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Black, 1.0f, 0);
            // render the terrain and skydome onto the render target (only the part below the clip plane will be rendered)
            DrawSkyDome(reflectionViewMatrix);
            DrawTerrain(reflectionViewMatrix);
            // turn clipping back off so the whole scene doesn't keep rendering as clipped
            effect.Parameters["Clipping"].SetValue(false);
            device.SetRenderTarget(null);
            // store the contents of the render target into the refractionMap texture
            reflectionMap = reflectionRenderTarget;
            // dump the texture to a file
            //DumpToFile(reflectionMap);
        }

        #endregion

        #region water drawing support
        VertexBuffer waterVertexBuffer;

        /// <summary>
        /// create the 2 huge triangles that span the whole terrain
        /// </summary>

        private void SetUpWaterVertices()
        {
            // use VertexPositionTexture vertices to be able to specify texture coordinates
            VertexPositionTexture[] waterVertices = new VertexPositionTexture[6];

            waterVertices[0] = new VertexPositionTexture(new Vector3(0, waterHeight, 0), new Vector2(0, 1));
            waterVertices[2] = new VertexPositionTexture(new Vector3(terrainWidth, waterHeight, -terrainLength), new Vector2(1, 0));
            waterVertices[1] = new VertexPositionTexture(new Vector3(0, waterHeight, -terrainLength), new Vector2(0, 0));

            waterVertices[3] = new VertexPositionTexture(new Vector3(0, waterHeight, 0), new Vector2(0, 1));
            waterVertices[5] = new VertexPositionTexture(new Vector3(terrainWidth, waterHeight, 0), new Vector2(1, 1));
            waterVertices[4] = new VertexPositionTexture(new Vector3(terrainWidth, waterHeight, -terrainLength), new Vector2(1, 0));

            waterVertexBuffer = new VertexBuffer(device, typeof(VertexPositionTexture), waterVertices.Length, BufferUsage.WriteOnly);
            waterVertexBuffer.SetData(waterVertices);
        }

        /// <summary>
        /// draw the 2 triangles, using the Water technique
        /// </summary>
        /// <param name="time"></param>
        private void DrawWater(float time)
        {
            effect.CurrentTechnique = effect.Techniques["Water"];
            Matrix worldMatrix = Matrix.Identity;
            effect.Parameters["xWorld"].SetValue(worldMatrix);
            effect.Parameters["xView"].SetValue(viewMatrix);
            effect.Parameters["xReflectionView"].SetValue(reflectionViewMatrix);
            effect.Parameters["xProjection"].SetValue(projectionMatrix);
            // pass maps to shader
            effect.Parameters["xReflectionMap"].SetValue(reflectionMap);
            effect.Parameters["xRefractionMap"].SetValue(refractionMap);
            effect.Parameters["xWaterBumpMap"].SetValue(waterBumpMap);
            // pass ripple params to shader
            effect.Parameters["xWaveLength"].SetValue(0.1f);
            effect.Parameters["xWaveHeight"].SetValue(0.3f);
            // pass camera position
            effect.Parameters["xCamPos"].SetValue(cameraPosition);
            // pass moving water params
            effect.Parameters["xTime"].SetValue(time);
            effect.Parameters["xWindForce"].SetValue(0.002f);
            effect.Parameters["xWindDirection"].SetValue(windDirection);


            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();

                device.SetVertexBuffer(waterVertexBuffer);
                int noVertices = waterVertexBuffer.VertexCount;
                device.DrawPrimitives(PrimitiveType.TriangleList, 0, noVertices / 3);
            }
        }
        #endregion

        #region water ripples support
        // texture for bump map to be passed to shader
        Texture2D waterBumpMap;

        #endregion

        #region wind support
        Vector3 windDirection = new Vector3(1, 0, 0);
        #endregion

        #region output support
        /// <summary>
        /// Output water textures in a small view port
        /// </summary>
        /// <param name="refractionMap">refraction texture to be displayed</param>
        /// <param name="reflectionMap">reflection texture to be displayed</param>
        void DrawViewPort(Texture2D refractionMap, Texture2D reflectionMap)
        {
            SpriteBatch spriteBatch = new SpriteBatch(GraphicsDevice);
            spriteBatch.Begin();
            Vector2 pos = new Vector2(graphics.PreferredBackBufferWidth - (graphics.PreferredBackBufferWidth / 10), 0);
            spriteBatch.Draw(refractionMap, pos, null, Color.White, 0f, Vector2.Zero, .1F, SpriteEffects.None, 0f);
            Vector2 pos2 = new Vector2(graphics.PreferredBackBufferWidth - (graphics.PreferredBackBufferWidth / 10), graphics.PreferredBackBufferHeight / 10);
            spriteBatch.Draw(reflectionMap, pos2, null, Color.White, 0f, Vector2.Zero, .1F, SpriteEffects.None, 0f);
            spriteBatch.End();
        }

        /// <summary>
        /// Output a texture to a jpeg file
        /// </summary>
        /// <param name="texture">texture to be saved</param>
        void DumpToFile(Texture2D texture)
        {
            System.IO.Stream ss = System.IO.File.OpenWrite("C:\\Test\\Refraction.jpg");
            texture.SaveAsJpeg(ss, 500, 500);
            ss.Close();
        }
        #endregion

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            graphics.PreferredBackBufferWidth = 1024;
            graphics.PreferredBackBufferHeight = 768;

            graphics.ApplyChanges();
            Window.Title = "3D Landscape";

            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load all of the content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.

            device = GraphicsDevice;

            // position the mouse in the middle and store this state
            Mouse.SetPosition(device.Viewport.Width / 2, device.Viewport.Height / 2);
            originalMouseState = Mouse.GetState();

            effect = Content.Load<Effect>("Series4Effects");

            UpdateViewMatrix();
            // The first argument defines the position of the camera. We position it 50 units on the positive Z axis.
            // The next parameter sets the target point the camera is looking at. We will be looking at our (0,0,0) 3D origin.
            // At this point, we have defined the viewing axis of our camera, but we can still rotate our camera around this axis.
            // So we still need to define which vector will be considered as 'up'.
            projectionMatrix = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, device.Viewport.AspectRatio, 0.3f, 1000.0f);

            // load skydome model from file, and replace its effect with our own
            skyDome = Content.Load<Model>("dome");
            skyDome.Meshes[0].MeshParts[0].Effect = effect.Clone();

            // initialize render targets for refraction ad reflection maps
            PresentationParameters pp = device.PresentationParameters;
            refractionRenderTarget = new RenderTarget2D(device, pp.BackBufferWidth, pp.BackBufferHeight, false, pp.BackBufferFormat, pp.DepthStencilFormat);
            reflectionRenderTarget = new RenderTarget2D(device, pp.BackBufferWidth, pp.BackBufferHeight, false, pp.BackBufferFormat, pp.DepthStencilFormat);

            LoadVertices();
            LoadTextures();
        }
        
        /// <summary>
        /// Initialize all our vertices and indices
        /// </summary>
        private void LoadVertices()
        {
            Texture2D heightMap = Content.Load<Texture2D>("heightmap");
            LoadHeightData(heightMap);

            // generates the vertices and indices for a terrain
            // these vertices contain both positional and color information
            VertexMultitextured[] terrainVertices = SetUpTerrainVertices();
            int[] terrainIndices = SetUpTerrainIndices();
            // generate the correct normals and add them to the vertices
            terrainVertices = CalculateNormals(terrainVertices, terrainIndices);
            // store entities in corresponding buffers for optimal speed
            CopyToTerrainBuffers(terrainVertices, terrainIndices);
            SetUpWaterVertices();
        }

        /// <summary>
        /// Height map color support
        /// </summary>
        /// <param name="heightMap">Greyscale image storing height of each pixel</param>
        private void LoadHeightData(Texture2D heightMap)
        {
            float minimumHeight = float.MaxValue;
            float maximumHeight = float.MinValue;

            terrainWidth = heightMap.Width;
            terrainLength = heightMap.Height;

            Color[] heightMapColors = new Color[terrainWidth * terrainLength];
            heightMap.GetData(heightMapColors);

            heightData = new float[terrainWidth, terrainLength];
            for (int x = 0; x < terrainWidth; x++)
                for (int y = 0; y < terrainLength; y++)
                {
                    heightData[x, y] = heightMapColors[x + y * terrainWidth].R;
                    if (heightData[x, y] < minimumHeight) minimumHeight = heightData[x, y];
                    if (heightData[x, y] > maximumHeight) maximumHeight = heightData[x, y];
                }

            for (int x = 0; x < terrainWidth; x++)
                for (int y = 0; y < terrainLength; y++)
                    heightData[x, y] = (heightData[x, y] - minimumHeight) / (maximumHeight - minimumHeight) * 30.0f;
        }

        /// <summary>
        /// Set up vertices to display passing in the texture coordinate
        /// </summary>
        private VertexMultitextured[] SetUpTerrainVertices()
        {
            VertexMultitextured[] terrainVertices = new VertexMultitextured[terrainWidth * terrainLength];

            for (int x = 0; x < terrainWidth; x++)
            {
                for (int y = 0; y < terrainLength; y++)
                {
                    terrainVertices[x + y * terrainWidth].Position = new Vector3(x, heightData[x, y], -y);
                    terrainVertices[x + y * terrainWidth].TextureCoordinate.X = (float)x / 30.0f;
                    terrainVertices[x + y * terrainWidth].TextureCoordinate.Y = (float)y / 30.0f;

                    // In the LoadHeightData, the heights of our vertices are set to values between 0 and 30.
                    // the vertices with height 0 should have weight=1 for the sand texture, while the vertices with height 30 should have weight=1
                    // for the snow texture. A vertex with height=25 should get a weight between 0 and 1 for both the snow and the rock texture.
                    terrainVertices[x + y * terrainWidth].TexWeights.X = MathHelper.Clamp(1.0f - Math.Abs(heightData[x, y] - 0) / 8.0f, 0, 1);
                    terrainVertices[x + y * terrainWidth].TexWeights.Y = MathHelper.Clamp(1.0f - Math.Abs(heightData[x, y] - 12) / 6.0f, 0, 1);
                    terrainVertices[x + y * terrainWidth].TexWeights.Z = MathHelper.Clamp(1.0f - Math.Abs(heightData[x, y] - 20) / 6.0f, 0, 1);
                    terrainVertices[x + y * terrainWidth].TexWeights.W = MathHelper.Clamp(1.0f - Math.Abs(heightData[x, y] - 30) / 6.0f, 0, 1);

                    // make sure that for every vertex, the sum of all weights is exactly 1.
                    float total = terrainVertices[x + y * terrainWidth].TexWeights.X;
                    total += terrainVertices[x + y * terrainWidth].TexWeights.Y;
                    total += terrainVertices[x + y * terrainWidth].TexWeights.Z;
                    total += terrainVertices[x + y * terrainWidth].TexWeights.W;

                    terrainVertices[x + y * terrainWidth].TexWeights.X /= total;
                    terrainVertices[x + y * terrainWidth].TexWeights.Y /= total;
                    terrainVertices[x + y * terrainWidth].TexWeights.Z /= total;
                    terrainVertices[x + y * terrainWidth].TexWeights.W /= total;
                }
            }
            return terrainVertices;
        }

        // multiple triangles support
        private int[] SetUpTerrainIndices()
        {
            int[] indices = new int[(terrainWidth - 1) * (terrainLength - 1) * 6];
            int counter = 0;
            for (int y = 0; y < terrainLength - 1; y++)
            {
                for (int x = 0; x < terrainWidth - 1; x++)
                {
                    int lowerLeft = x + y * terrainWidth;
                    int lowerRight = (x + 1) + y * terrainWidth;
                    int topLeft = x + (y + 1) * terrainWidth;
                    int topRight = (x + 1) + (y + 1) * terrainWidth;

                    indices[counter++] = topLeft;
                    indices[counter++] = lowerRight;
                    indices[counter++] = lowerLeft;

                    indices[counter++] = topLeft;
                    indices[counter++] = topRight;
                    indices[counter++] = lowerRight;
                }
            }
            return indices;
        }

        private VertexMultitextured[] CalculateNormals(VertexMultitextured[] vertices, int[] indices)
        {
            // clear the normals in each of your vertices
            for (int i = 0; i < vertices.Length; i++)
                vertices[i].Normal = new Vector3(0, 0, 0);

            // for each triangle, calculate its normal and add it to of each of the 3 vertices' normals
            for (int i = 0; i < indices.Length / 3; i++)
            {
                int index1 = indices[i * 3];
                int index2 = indices[i * 3 + 1];
                int index3 = indices[i * 3 + 2];

                Vector3 side1 = vertices[index1].Position - vertices[index3].Position;
                Vector3 side2 = vertices[index1].Position - vertices[index2].Position;
                Vector3 normal = Vector3.Cross(side1, side2);

                vertices[index1].Normal += normal;
                vertices[index2].Normal += normal;
                vertices[index3].Normal += normal;
            }

            // normalize all normals
            for (int i = 0; i < vertices.Length; i++)
                vertices[i].Normal.Normalize();

            return vertices;
        }

        private void CopyToTerrainBuffers(VertexMultitextured[] vertices, int[] indices)
        {
            terrainVertexBuffer = new VertexBuffer(device, typeof(VertexMultitextured), vertices.Length, BufferUsage.WriteOnly);
            terrainVertexBuffer.SetData(vertices);

            terrainIndexBuffer = new IndexBuffer(device, typeof(int), indices.Length, BufferUsage.WriteOnly);
            terrainIndexBuffer.SetData(indices);
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();

            // process keyboard input
            float timeDifference = (float)gameTime.ElapsedGameTime.TotalMilliseconds / 1000.0f;
            ProcessInput(timeDifference);

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            float time = (float)gameTime.TotalGameTime.TotalMilliseconds / 100.0f;
            RasterizerState rs = new RasterizerState();
            rs.CullMode = CullMode.None; // turn culling off (i.e. draw all triangles, even those not facing the camera)
            //rs.FillMode = FillMode.WireFrame; // turn wire frame view on
            device.RasterizerState = rs;

            DrawRefractionMap();
            DrawReflectionMap();
            // set background and Z-buffering
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Black, 1.0f, 0);
            DrawSkyDome(viewMatrix);
            DrawTerrain(viewMatrix);
            DrawWater(time);
            // draw the viewport
            DrawViewPort(refractionMap, reflectionMap);

            base.Draw(gameTime);
        }
    }
}