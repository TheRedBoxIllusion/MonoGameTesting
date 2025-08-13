using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using static System.Net.Mime.MediaTypeNames;

namespace MonoGameTesting
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        int pixelsPerBlock = 16;
        int[,] blockList;
        List<Texture2D> blockIds;

        WorldContext worldContext;

        (int x, int y) screenOffset;

        //World generation constants
        

        public Texture2D textureFromColor(Color col) {
            Texture2D _blankTexture = new Texture2D(_spriteBatch.GraphicsDevice, 1,1);
            _blankTexture.SetData(new[] {col});
            return _blankTexture;
        }

        public Game1()
        {
            (int width, int height) windowDimensions = (900, 900);

            (int width, int height) worldDimensions = (512, 512);

            //Perlin Noise Variables:
            int noiseIterations = 8;

            double[] octaveWeights = new double[noiseIterations];
            octaveWeights[0] = 1.1;
            octaveWeights[1] = 0.75;
            octaveWeights[2] = 0.095;
            octaveWeights[3] = 0.0625;
            octaveWeights[4] = 0.03;
            octaveWeights[5] = 0.015;
            octaveWeights[6] = 0.0075;
            octaveWeights[7] = 0.00325;

            double frequency = 0.045;

            //Higher means more solid
            double blockThreshold = 0.45;
            double decreasePerY = 0.0001;
            double maximumThreshold = 0.45;//Only decreasing so...
            double minimumThreshold = 0.4;

            int vectorCount = 15;

            //SeededBrownianMotion Variables:
            BlockGenerationVariables[] ores = {
            new BlockGenerationVariables(1, 1, 8, 360),
            new BlockGenerationVariables(0.1, 3, 1, 4, (0.3, 0.6, 0.1, 0.0, 0.0, 0.0, 0.0, 0.0)),
            new BlockGenerationVariables(0.4, 2, 2, 40)
            };
            int maxAttempts = 15;

            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            worldContext = new WorldContext();
            worldContext.generateWorld(worldDimensions, noiseIterations, octaveWeights, frequency, vectorCount, blockThreshold, decreasePerY, maximumThreshold, minimumThreshold, ores, maxAttempts);
            
        }

        protected override void Initialize()
        {
            //_graphics.IsFullScreen = true;
            _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            _graphics.ApplyChanges();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // TODO: use this.Content to load your game content here
            worldContext.setBlockList(new List<Block>
            {
            new Block(textureFromColor(Color.White)), //Air: ID = 0
            new Block(new List<Texture2D>{Content.Load<Texture2D>("Stone1"), Content.Load<Texture2D>("Stone2")}), // Stone: ID = 1
            new Block(textureFromColor(Color.Blue)), //Dirt: ID = 2
            new Block(textureFromColor(Color.Red), 255, (1, 0, 0)) //Ore: ID = 3
            });

            worldContext.calculateInitialLightLevels();

        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            if(Keyboard.GetState().IsKeyDown(Keys.D)){
                screenOffset.x += 5;
            }
            if (Keyboard.GetState().IsKeyDown(Keys.A))
            {
                screenOffset.x -= 5;
            }
            if (Keyboard.GetState().IsKeyDown(Keys.S))
            {
                screenOffset.y += 5;
            }
            if (Keyboard.GetState().IsKeyDown(Keys.W))
            {
                screenOffset.y -= 5;
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            int[,] tempBlockArray = worldContext.blockArray;

            (int x, int y) screenOffsetInBlocks = ((int)Math.Floor((double)screenOffset.x / pixelsPerBlock), (int)Math.Floor((double)screenOffset.y/pixelsPerBlock));
            (int x, int y) remainderOffset = (screenOffset.x - screenOffsetInBlocks.x * pixelsPerBlock, screenOffset.y - screenOffsetInBlocks.y * pixelsPerBlock);
            _spriteBatch.Begin();
            for (int x = 0; x < GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width/pixelsPerBlock + 2; x++) {
                for (int y = 0; y < GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height/pixelsPerBlock + 2; y++) {
                    if (x + screenOffsetInBlocks.x >= 0 && y + screenOffsetInBlocks.y >= 0 && x + screenOffsetInBlocks.x < tempBlockArray.GetLength(0) && y + screenOffsetInBlocks.y < tempBlockArray.GetLength(1))
                    {
                        
                            (int r, int g, int b) lightLevel = worldContext.lightLevelArray[x + screenOffsetInBlocks.x, y + screenOffsetInBlocks.y];
                        
                            _spriteBatch.Draw(worldContext.getBlockFromID(tempBlockArray[x + screenOffsetInBlocks.x, y + screenOffsetInBlocks.y]).getTexture((x + screenOffsetInBlocks.x) + (3 * tempBlockArray.GetLength(0) + 56) * (y + screenOffsetInBlocks.y)), new Rectangle(pixelsPerBlock * x - remainderOffset.x, pixelsPerBlock * y - remainderOffset.y, pixelsPerBlock, pixelsPerBlock), new Color(lightLevel.r, lightLevel.g, lightLevel.b));
                    }
                }
            }
            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }

    public class WorldContext
    {
        double[,] perlinNoiseArray;
        BlockGenerationVariables[,] brownianMotionArray;

        List<Block> blockList;
        public int[,] blockArray { get; set; }

        public (int r, int g, int b)[,] lightLevelArray { get; set; } //0-255 value for the light level of each block. the 3 values are the color of the light The blockSizeInMeters variable controls how slowly the light falls off

        int[,] oreArray;

        public int[,] getBlockArray()
        {
            return blockArray;
        }

        public List<Block> getIDList() {
            return blockList;

        }

        public void setBlockList(List<Block> blocks) {
            blockList = blocks;
        }


        public int[,] getOreArray()
        {
            return oreArray;
        }

        public Block getBlockFromID(int ID)
        {
            return blockList[ID];
        }

        public void generateWorld((int width, int height) worldDimensions, int perlinNoiseIterations, double[] octaveWeights, double frequency, int vectorCount, double blockThreshold, double changePerY, double maximumThreshold, double minimumThreshold, BlockGenerationVariables[] oresArray, int brownianAttemptCount)
        {
            perlinNoiseArray = new double[worldDimensions.width, worldDimensions.height];
            brownianMotionArray = new BlockGenerationVariables[worldDimensions.width, worldDimensions.height];
            blockArray = new int[worldDimensions.width, worldDimensions.height];
            oreArray = new int[worldDimensions.width, worldDimensions.height];
            lightLevelArray = new (int, int, int)[worldDimensions.width, worldDimensions.height];

            perlinNoise(worldDimensions, perlinNoiseIterations, octaveWeights, frequency, vectorCount);
            seededBrownianMotion(oresArray, brownianAttemptCount);
            combineAlgorithms(blockThreshold, changePerY, maximumThreshold, minimumThreshold);
            generateOreArray(brownianMotionArray);
            
        }

        public void calculateInitialLightLevels() {
            double blockSizeInMeters = 0.05;
            for (int xBlock = 0; xBlock < blockArray.GetLength(0); xBlock++) {
                for (int yBlock = 0; yBlock < blockArray.GetLength(1); yBlock++)
                {
                    if (getBlockFromID(blockArray[xBlock, yBlock]).lightPower > 0) {
                        int blockLightPower = getBlockFromID(blockArray[xBlock, yBlock]).lightPower;
                        (int r, int g, int b) colorWeights = getBlockFromID(blockArray[xBlock, yBlock]).colorWeights;
                        System.Diagnostics.Debug.WriteLine(colorWeights);
                        //calculate the light's effect radius using I = power/4Pi * r^2 where I = 1 (When Math.Floor goes to 0)
                        //R = Sqrt power/4PII
                        int Radius = (int)Math.Sqrt(blockLightPower/(4 * Math.PI * blockSizeInMeters));
                        for (int x = -Radius; x < Radius; x++) {
                            for (int y = -Radius; y < Radius; y++) {
                                if (xBlock + x >= 0 && yBlock + y >= 0 && xBlock + x < lightLevelArray.GetLength(0) && yBlock + y < lightLevelArray.GetLength(1))
                                {
                                    double currentRadius = Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2));
                                    if (currentRadius < Radius)  //Circle area
                                    {
                                        double lightPenetration;
                                        if (calculateRayPath((xBlock + x, yBlock + y), (xBlock, yBlock))){
                                            lightPenetration = 0.35; // Make the light penetrate half as far
                                        } else {
                                            lightPenetration = 0;
                                        }
                                        int lightLevelPower = (int)(blockLightPower / (4 * Math.PI * Math.Pow(currentRadius, 2) * (blockSizeInMeters + lightPenetration) + 0.1)); //+0.1 to prevent divide by 0 error, will immediately get canceled out by the (int)
                                        lightLevelArray[xBlock + x, yBlock + y] = (colorWeights.r * lightLevelPower, colorWeights.g * lightLevelPower, colorWeights.b * lightLevelPower);

                                        if (lightLevelArray[xBlock + x, yBlock + y].r > 255)
                                        {
                                            lightLevelArray[xBlock + x, yBlock + y].r = 255;
                                        } else if (lightLevelArray[xBlock + x, yBlock + y].g > 255)
                                        {
                                            lightLevelArray[xBlock + x, yBlock + y].g = 255;
                                        } else if (lightLevelArray[xBlock + x, yBlock + y].b > 255)
                                        {
                                            lightLevelArray[xBlock + x, yBlock + y].b = 255;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

            }
        }

        public bool calculateRayPath((int x, int y) destination, (int x, int y) source)
        {
            bool interference = false;
            //Go through every block in between destination and source
            //Calculate the radius
            if (!(destination.x == source.x && destination.y == source.y))
            {
                int Radius = (int)(Math.Sqrt(Math.Pow(destination.x - source.x, 2) + Math.Pow(destination.y - source.y, 2)));
                (int x, int y) segmentDifference = ((destination.x - source.x) / Radius, (destination.y - source.y) / Radius);

                //Say radius equals 4. take the difference in their values, and for int i - radius, multiply, get the block from the list and check if it's not air
                for (int i = 1; i < Radius + 1; i++)
                {
                    if (blockArray[source.x + i * segmentDifference.x, source.y + i * segmentDifference.y] != 0)
                    {
                        
                        interference = true;
                        break;
                    }
                }
            }
            

            return interference;
        }

        public void generateOreArray(BlockGenerationVariables[,] ores)
        {
            for (int x = 0; x < ores.GetLength(0); x++)
            {
                for (int y = 0; y < ores.GetLength(1); y++)
                {
                    if (ores[x, y] != null)
                    {
                        oreArray[x, y] = ores[x, y].block;
                    }
                    else
                    {
                        oreArray[x, y] = 0; //Set to Air
                    }
                }
            }
        }

        public void perlinNoise((int width, int height) worldDimensions, int perlinNoiseIterations, double[] octaveWeights, double frequency, int vectorCount)
        {
            PerlinNoise pn = new PerlinNoise(worldDimensions, perlinNoiseIterations, vectorCount);
            int[] g = generateRandomIntArray();
            perlinNoiseArray = pn.generatePerlinNoise(g, worldDimensions, octaveWeights, frequency);
        }

        public int[] generateRandomIntArray()
        {
            int[] initialArray = new int[256];
            List<int> sortedArray = new List<int>();

            for (int i = 0; i < initialArray.Count(); i++)
            {
                sortedArray.Add(i);
            }
            for (int i = 0; i < initialArray.Count(); i++)
            {
                Random r = new Random();
                int rIndex = r.Next(0, sortedArray.Count());
                initialArray[i] = sortedArray[rIndex];
                sortedArray.RemoveAt(rIndex);
            }

            int[] outputArray = new int[initialArray.Count() * 2];
            for (int i = 0; i < outputArray.Count(); i++)
            {
                outputArray[i] = initialArray[i % 255];
            }

            return outputArray;
        }
        public void seededBrownianMotion(BlockGenerationVariables[] oresArray, int attemptCount)
        {
            SeededBrownianMotion sbm = new SeededBrownianMotion();
            brownianMotionArray = sbm.seededBrownianMotion(brownianMotionArray, oresArray);
            brownianMotionArray = sbm.brownianAlgorithm(brownianMotionArray, attemptCount);
        }
        public void combineAlgorithms(double blockThreshold, double changePerY, double maximumThreshold, double minimumThreshold)
        {
            for (int x = 0; x < blockArray.GetLength(0); x++)
            {
                for (int y = 0; y < blockArray.GetLength(1); y++)
                {
                    if (perlinNoiseArray[x, y] > changeThresholdByDepth(blockThreshold, changePerY, y, maximumThreshold, minimumThreshold))
                    { //If it's above the block threshold, set the block to be air, 
                        blockArray[x, y] = 0;
                    }
                    else if (brownianMotionArray[x, y] != null)
                    {
                        blockArray[x, y] = brownianMotionArray[x, y].block;
                    }
                    else
                    {
                        blockArray[x, y] = 0;
                    }
                }
            }
        }

        public double changeThresholdByDepth(double blockThreshold, double changePerY, double y, double maximumThreshold, double minimumThreshold)
        {
            blockThreshold = blockThreshold - changePerY * y;
            if (blockThreshold > maximumThreshold)
            {
                blockThreshold = maximumThreshold;
            }
            else if (blockThreshold < minimumThreshold)
            {
                blockThreshold = minimumThreshold;
            }

            return blockThreshold;
        }


    }

    public class Block
    {
        public Color color;
        public Texture2D texture;

        public (int r, int g, int b) colorWeights { get; set; } = (1, 1, 1);

        public int lightPower { get; set; } = 0;  //A value between 0-15 indicating the light given off by a block

        public List<Texture2D> textureList;

        int[] pseudorandomArray = new int[256];

        public Block(Color color)
        {
            this.color = color;
        }
        public Block(Block b)
        {
            color = b.color;
            texture = b.texture;
            textureList = b.textureList;
            lightPower = b.lightPower;
            colorWeights = b.colorWeights;
        }
        public Block(Texture2D texture)
        {
            this.texture = texture;
        }
        public Block(List<Texture2D> textures)
        {
            textureList = textures;
            generatePseudorandomArray();
        }

        //In real builds, emmisive blocks will have their own class, instead of a second set of constructors.
        public Block(Texture2D texture, int light)
        {
            this.texture = texture;
            lightPower = light;
            
        }
        public Block(List<Texture2D> textures, int light)
        {
            textureList = textures;
            lightPower = light;
            
            generatePseudorandomArray();
        }
        public Block(Texture2D texture, int light, (int r, int g, int b) colorWeights)
        {
            this.texture = texture;
            lightPower = light;
            this.colorWeights = colorWeights;
        }
        public Block(List<Texture2D> textures, int light, (int r, int g, int b) colorWeight)
        {
            textureList = textures;
            lightPower = light;
            colorWeights = colorWeight;
            generatePseudorandomArray();
        }

        public void generatePseudorandomArray() {
            for (int i = 0; i < pseudorandomArray.Length; i++) {
                pseudorandomArray[i] = i % textureList.Count;
            }
            Random r = new Random();
            r.Shuffle(pseudorandomArray);
        }

        public Texture2D getTexture(int position)
        {
            if (textureList != null)
            {
                if (textureList.Count == 0)
                {
                    return texture;
                }
                else
                {
                    return textureList[pseudorandomArray[position%256]];
                }
            }
            else
            {
                return texture;
            }
        }
    }

    public class BlockGenerationVariables
    {
        public double seedDensity;
        public int block;
        public int maxSingleSpread;
        public int currentSingleSpread;
        public int oreVeinSpread;

        public int identifier;

        public List<BlockGenerationVariables> veinList = new List<BlockGenerationVariables>();

        public (double north, double northEast, double east, double southEast, double south, double southWest, double west, double northWest) directionWeights = (0.125, 0.125, 0.125, 0.125, 0.125, 0.125, 0.125, 0.125); //Perfectly weighted as default
        public BlockGenerationVariables(double seedDensity, int block, int maxSingleSpread, int oreVeinSpread, (double north, double northEast, double east, double southEast, double south, double southWest, double west, double northWest) directionWeights)
        {
            this.seedDensity = seedDensity;
            this.block = block;
            this.maxSingleSpread = maxSingleSpread;
            this.currentSingleSpread = maxSingleSpread;
            this.oreVeinSpread = oreVeinSpread;
            this.directionWeights = directionWeights;

        }

        public BlockGenerationVariables(double seedDensity, int block, int maxSingleSpread, int oreVeinSpread)
        {
            this.seedDensity = seedDensity;
            this.block = block;
            this.maxSingleSpread = maxSingleSpread;
            this.currentSingleSpread = maxSingleSpread;
            this.oreVeinSpread = oreVeinSpread;
        }

        public BlockGenerationVariables(BlockGenerationVariables blockVariables)
        {
            seedDensity = blockVariables.seedDensity;
            block = blockVariables.block;
            maxSingleSpread = blockVariables.maxSingleSpread;
            currentSingleSpread = blockVariables.maxSingleSpread;
            oreVeinSpread = blockVariables.oreVeinSpread;
            directionWeights = blockVariables.directionWeights;
            veinList = blockVariables.veinList;
            identifier = blockVariables.identifier + 2;
        }

        public void hasSpread()
        {
            currentSingleSpread -= 1;
            oreVeinSpread -= 1;
        }

        public void hasSpreadVein(int oreVeinSpread)
        {
            this.oreVeinSpread = oreVeinSpread;
        }

        public void initialiseVeinList(BlockGenerationVariables thisBlock)
        {
            veinList = new List<BlockGenerationVariables>();
            veinList.Add(thisBlock);
        }

        public void updateVeinList(BlockGenerationVariables newBlock)
        {
            if (!veinList.Contains(newBlock))
            {
                veinList.Add(newBlock);
            }
        }

    }

    public class PerlinNoise
    {
        List<double[,]> pixelOctaves = new List<double[,]>();

        Vector2[] randomisedUnitVectors;
        public PerlinNoise((int outputSizeX, int outputSizeY) outputDimensions, int octaveCount, int vectorCount)
        {
            randomisedUnitVectors = new Vector2[vectorCount];
            for (int octaves = 0; octaves < octaveCount; octaves++)
            {
                pixelOctaves.Add(new double[outputDimensions.outputSizeX, outputDimensions.outputSizeY]);
            }
        }

        public void randomiseVectorArray(int[] g)
        {
            double radiansPerIndex = 2 * Math.PI / randomisedUnitVectors.Count();

            for (int i = 0; i < randomisedUnitVectors.Count(); i++)
            {
                randomisedUnitVectors[i] = new Vector2((float)Math.Cos(radiansPerIndex * g[i]), (float)Math.Sin(radiansPerIndex * g[i]));
            }
        }


        public double[,] generatePerlinNoise(int[] g, (int noiseOutputSizeX, int noiseOutputSizeY) outputDimensions, double[] octaveWeights, double frequency)
        {
            double[,] noiseOutput = new double[outputDimensions.noiseOutputSizeX, outputDimensions.noiseOutputSizeY];

            randomiseVectorArray(g);

            for (int i = 0; i < pixelOctaves.Count(); i++)
            {
                //pixelOctaves[i] = randomlyInitialisePixelArray(pixelOctaves[i]);
                pixelOctaves[i] = perlinAlgorithm(pixelOctaves[i], frequency * Math.Pow(2, i), g);
                noiseOutput = addNoiseToOutput(noiseOutput, pixelOctaves[i], octaveWeights[i]);
            }


            return noiseOutput;
        }

        public double[,] addNoiseToOutput(double[,] currentNoise, double[,] newNoise, double octaveWeight)
        {
            double[,] cumulatedNoise = new double[currentNoise.GetLength(0), currentNoise.GetLength(1)];

            for (int x = 0; x < currentNoise.GetLength(0); x++)
            {
                for (int y = 0; y < currentNoise.GetLength(1); y++)
                {
                    double cumulateNoise = currentNoise[x, y] + octaveWeight * newNoise[x, y];
                    double boundedNoise = cumulateNoise / (1 + octaveWeight);
                    cumulatedNoise[x, y] = boundedNoise;

                }
            }

            return cumulatedNoise;
        }
        public double[,] perlinAlgorithm(double[,] pixels, double frequency, int[] g)
        {
            for (int x = 0; x < pixels.GetLength(0); x++)
            {
                for (int y = 0; y < pixels.GetLength(1); y++)
                {

                    //Multiply the location by a small 'frequency' value
                    Vector2 location = new Vector2((float)frequency * x, (float)frequency * y);

                    int X = (int)Math.Floor(location.X) % 255;
                    int Y = (int)Math.Floor(location.Y) % 255;

                    float xlocal = (float)(location.X - Math.Floor(location.X));
                    float ylocal = (float)(location.Y - Math.Floor(location.Y));

                    Vector2 topLeft = new Vector2(xlocal, ylocal);
                    Vector2 topRight = new Vector2(xlocal - 1, ylocal);
                    Vector2 bottomLeft = new Vector2(xlocal, ylocal - 1);
                    Vector2 bottomRight = new Vector2(xlocal - 1, ylocal - 1);

                    int topLeftValue = g[g[X] + Y];
                    int topRightValue = g[g[X + 1] + Y];
                    int bottomLeftValue = g[g[X] + Y + 1];
                    int bottomRightValue = g[g[X + 1] + Y + 1];

                    double dotTopLeft = Vector2.Dot(topLeft, getConstantVector(topLeftValue));
                    double dotTopRight = Vector2.Dot(topRight, getConstantVector(topRightValue));
                    double dotBottomLeft = Vector2.Dot(bottomLeft, getConstantVector(bottomLeftValue));
                    double dotBottomRight = Vector2.Dot(bottomRight, getConstantVector(bottomRightValue));



                    double xf = fadeFunction(xlocal);
                    double yf = fadeFunction(ylocal);

                    double noise = Lerp(xf,
                    Lerp(yf, dotTopLeft, dotBottomLeft),
                    Lerp(yf, dotTopRight, dotBottomRight));

                    if (noise > 1)
                    {
                        Console.WriteLine("At this pixel-");
                        Console.WriteLine("Noise: " + noise);
                        Console.WriteLine("xf & yf: " + xf + ", " + yf);
                    }

                    pixels[x, y] = (noise + 1) / 2;
                }
            }

            return pixels;
        }

        public Vector2 getConstantVector(int value)
        {
            Vector2 constantVector;

            constantVector = randomisedUnitVectors[value % randomisedUnitVectors.Count()];

            return constantVector;
        }

        public double fadeFunction(double t)
        {
            //Perlins improved fade function: 6^t5 -15t^4 +10t^3
            double fadeFunctionValue = 6.0 * Math.Pow(t, 5) - 15.0 * Math.Pow(t, 4) + 10.0 * Math.Pow(t, 3);
            return fadeFunctionValue;
        }

        public double Lerp(double t, double v1, double v2)
        {
            double lerp = v1 + t * (v2 - v1);

            return lerp;
        }

    }

    public class SeededBrownianMotion
    {
        public BlockGenerationVariables[,] seededBrownianMotion(BlockGenerationVariables[,] worldArray, BlockGenerationVariables[] ores)
        {
            BlockGenerationVariables[,] seededArray = new BlockGenerationVariables[worldArray.GetLength(0), worldArray.GetLength(1)];
            seededArray = seedArray(worldArray, ores);
            //seededArray = BrownianAlgorithm(seededArray);
            return seededArray;
        }

        public BlockGenerationVariables[,] seedArray(BlockGenerationVariables[,] worldArray, BlockGenerationVariables[] ores)
        {
            //Generate a random number of seeds for each ore, depending on it's seedDensity, 
            //then randomly distribute them inside the world Array
            foreach (BlockGenerationVariables ore in ores)
            {
                int numberOfSeeds = (int)((ore.seedDensity / 100) * worldArray.Length);
                for (int i = 0; i < numberOfSeeds; i++)
                {
                    Random r = new Random();
                    int seedX = r.Next(0, worldArray.GetLength(0) - 1);
                    int seedY = r.Next(0, worldArray.GetLength(1) - 1);
                    //Creates a new class with the same parameters. If it directly equals it just passes a pointer
                    BlockGenerationVariables newBlock = new BlockGenerationVariables(ore);
                    newBlock.initialiseVeinList(newBlock); //Add itself to the Veinlist Array
                    newBlock.identifier = i;
                    worldArray[seedX, seedY] = newBlock;
                }
            }

            return worldArray;
        }

        public BlockGenerationVariables[,] brownianAlgorithm(BlockGenerationVariables[,] worldArray, int attemptCount)
        {
            //It would probably be more efficient to have a seperate array containing only the non-null blocks but I don't know a
            //readable way of doing it (ironic with the line break)
            int attempts = 0;

            while (attempts < attemptCount) //Runs until no changes have been made in that iteration.
            {
                Console.WriteLine("Iterated!");
                bool hasChangedTheArray = false;
                //Read everything from the worldArray but write to the tempArray then equalise at the end
                BlockGenerationVariables[,] tempArray = worldArray.Clone() as BlockGenerationVariables[,];

                for (int x = 0; x < worldArray.GetLength(0); x++)
                {
                    for (int y = 0; y < worldArray.GetLength(1); y++)
                    {
                        (BlockGenerationVariables[,] outputArray, bool hasChanged) output = brownianMotion(worldArray, tempArray, x, y);
                        tempArray = output.outputArray;
                        if (output.hasChanged && !hasChangedTheArray)
                        {
                            hasChangedTheArray = true;
                        }
                    }

                }

                worldArray = tempArray.Clone() as BlockGenerationVariables[,];
                if (!hasChangedTheArray)
                {
                    attempts += 1;
                }
            }

            fill(worldArray);

            return worldArray;
        }

        public (BlockGenerationVariables[,], bool) brownianMotion(BlockGenerationVariables[,] worldArray, BlockGenerationVariables[,] tempArray, int x, int y)
        {
            bool hasChanged = false;
            if (worldArray[x, y] != null)
            {
                BlockGenerationVariables block = worldArray[x, y];
                if (block.currentSingleSpread > 0 && block.oreVeinSpread > 0) //If the block/vein is allowed to spread
                {
                    Random r = new Random();
                    double rValue = r.NextDouble();
                    if (rValue <= block.directionWeights.north)
                    {
                        if (y - 1 >= 0)
                        {
                            if (tempArray[x, y - 1] == null)
                            {
                                tempArray[x, y - 1] = spreadBlock(worldArray, tempArray, (x, y));
                                hasChanged = true;
                            }

                        }
                    }
                    else if (rValue < block.directionWeights.north + block.directionWeights.northEast)
                    {
                        if (x + 1 < worldArray.GetLength(0) && y - 1 >= 0)
                        {
                            if (tempArray[x + 1, y - 1] == null)
                            {
                                tempArray[x + 1, y - 1] = spreadBlock(worldArray, tempArray, (x, y));
                                hasChanged = true;
                            }
                        }

                    }
                    else if (rValue < (block.directionWeights.north + block.directionWeights.northEast + block.directionWeights.east))
                    {
                        if (x + 1 < worldArray.GetLength(0))
                        {
                            if (tempArray[x + 1, y] == null)
                            {
                                tempArray[x + 1, y] = spreadBlock(worldArray, tempArray, (x, y));
                                hasChanged = true;
                            }
                        }

                    }
                    else if (rValue < (block.directionWeights.north + block.directionWeights.northEast + block.directionWeights.east + block.directionWeights.southEast))
                    {
                        if (y + 1 < worldArray.GetLength(1) && x + 1 < worldArray.GetLength(0)) //Make sure the block is inside the array bounds
                        {
                            if (tempArray[x + 1, y + 1] == null)
                            {
                                tempArray[x + 1, y + 1] = spreadBlock(worldArray, tempArray, (x, y));
                                hasChanged = true;
                            }
                        }

                    }

                    else if (rValue < (block.directionWeights.north + block.directionWeights.northEast + block.directionWeights.east + block.directionWeights.southEast + block.directionWeights.south))
                    {
                        if (y + 1 < worldArray.GetLength(1)) //Make sure the block is inside the array bounds
                        {
                            if (tempArray[x, y + 1] == null)
                            {
                                tempArray[x, y + 1] = spreadBlock(worldArray, tempArray, (x, y));
                                hasChanged = true;
                            }
                        }

                    }
                    else if (rValue < (block.directionWeights.north + block.directionWeights.northEast + block.directionWeights.east + block.directionWeights.southEast + block.directionWeights.south + block.directionWeights.southWest))
                    {
                        if (y + 1 < worldArray.GetLength(1) && x - 1 >= 0) //Make sure the block is inside the array bounds
                        {
                            if (tempArray[x - 1, y + 1] == null)
                            {
                                tempArray[x - 1, y + 1] = spreadBlock(worldArray, tempArray, (x, y));
                                hasChanged = true;
                            }
                        }

                    }
                    else if (rValue < (block.directionWeights.north + block.directionWeights.northEast + block.directionWeights.east + block.directionWeights.southEast + block.directionWeights.south + block.directionWeights.southWest + block.directionWeights.west))
                    {
                        if (x - 1 >= 0) //Make sure the block is inside the array bounds
                        {
                            if (tempArray[x - 1, y] == null)
                            {
                                tempArray[x - 1, y] = spreadBlock(worldArray, tempArray, (x, y));
                                hasChanged = true;
                            }
                        }

                    }
                    else
                    {
                        if (x - 1 >= 0 && y - 1 >= 0) //Make sure the block is inside the array bounds
                        {
                            if (tempArray[x - 1, y - 1] == null)
                            {
                                tempArray[x - 1, y - 1] = spreadBlock(worldArray, tempArray, (x, y));
                                hasChanged = true;
                            }
                        }

                    }
                }
            }
            return (tempArray, hasChanged);
        }

        public BlockGenerationVariables spreadBlock(BlockGenerationVariables[,] worldArray, BlockGenerationVariables[,] tempArray, (int x, int y) location)
        {
            int x = location.x;
            int y = location.y;
            foreach (BlockGenerationVariables b in worldArray[x, y].veinList)
            {
                b.oreVeinSpread -= 1;    //Sychronise the updated ore size across all the blocks in the vein
            }
            tempArray[x, y].currentSingleSpread -= 1;
            BlockGenerationVariables newBlock = new BlockGenerationVariables(worldArray[x, y]);
            newBlock.updateVeinList(newBlock);
            return newBlock;
        }

        public Block[,] fill(Block[,] blockArray)
        {
            for (int x = 0; x < blockArray.GetLength(0); x++)
            {
                for (int y = 0; y < blockArray.GetLength(1); y++)
                {
                    if (blockArray[x, y] == null)
                    {
                        List<Block> blocks = new List<Block>();
                        List<int> blockCount = new List<int>();
                        for (int xLocal = x - 1; xLocal <= x + 1; xLocal++)
                        {
                            for (int yLocal = y - 1; yLocal <= y + 1; yLocal++)
                            {
                                if (xLocal >= 0 && yLocal >= 0 && xLocal < blockArray.GetLength(0) && yLocal < blockArray.GetLength(1))
                                    if (blockArray[xLocal, yLocal] != null)
                                    {
                                        if (blocks.Contains(blockArray[xLocal, yLocal]))
                                        {
                                            blockCount[blocks.FindIndex(u => u.color == blockArray[xLocal, yLocal].color)] += 1;
                                        }
                                        else
                                        {
                                            blocks.Add(blockArray[xLocal, yLocal]);
                                            blockCount.Add(1);
                                        }
                                    }
                            }
                        }
                        if (blocks.Count != 0)
                        {
                            blockArray[x, y] = blocks[blockCount.IndexOf(blockCount.Max())];
                        }

                    }
                }
            }

            return blockArray;
        }

        public BlockGenerationVariables[,] fill(BlockGenerationVariables[,] blockArray)
        {
            for (int x = 0; x < blockArray.GetLength(0); x++)
            {
                for (int y = 0; y < blockArray.GetLength(1); y++)
                {
                    if (blockArray[x, y] == null)
                    {
                        List<int> blocks = new List<int>();
                        List<BlockGenerationVariables> blockVariables = new List<BlockGenerationVariables>();
                        List<int> blockCount = new List<int>();
                        for (int xLocal = x - 1; xLocal <= x + 1; xLocal++)
                        {
                            for (int yLocal = y - 1; yLocal <= y + 1; yLocal++)
                            {
                                if (xLocal >= 0 && yLocal >= 0 && xLocal < blockArray.GetLength(0) && yLocal < blockArray.GetLength(1))
                                    if (blockArray[xLocal, yLocal] != null)
                                    {
                                        if (blocks.Contains(blockArray[xLocal, yLocal].block))
                                        {
                                            blockCount[blocks.IndexOf(blockArray[xLocal, yLocal].block)] += 1;
                                        }
                                        else
                                        {
                                            blocks.Add(blockArray[xLocal, yLocal].block);
                                            blockVariables.Add(blockArray[xLocal, yLocal]);
                                            blockCount.Add(1);
                                        }
                                    }
                            }
                        }
                        if (blocks.Count != 0)
                        {
                            blockArray[x, y] = blockVariables[blockCount.IndexOf(blockCount.Max())];
                        }

                    }
                }
            }

            return blockArray;
        }

    }
}


