﻿//   
// Copyright (c) Jesse Freeman. All rights reserved.  
//  
// Licensed under the Microsoft Public License (MS-PL) License. 
// See LICENSE file in the project root for full license information. 
// 
// Contributors
// --------------------------------------------------------
// This is the official list of Pixel Vision 8 contributors:
//  
// Jesse Freeman - @JesseFreeman
// Christer Kaitila - @McFunkypants
// Pedro Medeiros - @saint11
// Shawn Rakowski - @shwany

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using PixelVisionRunner.Chips;
using PixelVisionRunner.Parsers;
using PixelVisionSDK;
using PixelVisionSDK.Services;

namespace PixelVisionRunner.Services
{

    public class LoadService : AbstractService
    {

        private readonly List<AbstractParser> parsers = new List<AbstractParser>();

        private int currentParserID;

        protected bool microSteps = false;
        protected AbstractParser parser;

        protected IEngine targetEngine;
        private int totalParsers;

        private ITextureFactory textureFactory;
        private IColorFactory colorFactory;

        public bool completed
        {
            get { return currentParserID >= totalParsers; }
        }

        public float percent
        {
            get { return currentParserID / (float) totalParsers; }
        }

        /// <summary>
        ///     This can be used to display a message while preloading
        /// </summary>
        public string message { get; protected set; }

        public LoadService(ITextureFactory textureFactory, IColorFactory colorFactory)
        {
            this.textureFactory = textureFactory;
            this.colorFactory = colorFactory;
        }

        public void ParseFiles(Dictionary<string, byte[]> files, IEngine engine, SaveFlags saveFlags)
        {
            parsers.Clear();
    
            var watch = Stopwatch.StartNew();
            
            // Save the engine so we can work with it during loading
            targetEngine = engine;

            // Step 1. Load the system snapshot
            if ((saveFlags & SaveFlags.System) == SaveFlags.System)
                LoadSystem(files);

            // Step 2 (optional). Load up the Lua script
            if ((saveFlags & SaveFlags.Code) == SaveFlags.Code)
            {
                var scriptExtension = ".lua";

                var paths = files.Keys.Where(s => s.EndsWith(scriptExtension)).ToList();


                foreach (var fileName in paths)
                {
                    parser = LoadScript(fileName, files[fileName]);
                    parsers.Add(parser);
                }
            }

            // Step 3 (optional). Look for new colors
            if ((saveFlags & SaveFlags.Colors) == SaveFlags.Colors)
            {
                parser = LoadColors(files);
                if (parser != null)
                    parsers.Add(parser);
            }

            // Step 4 (optional). Look for color map for sprites and tile map
            if ((saveFlags & SaveFlags.ColorMap) == SaveFlags.ColorMap)
            {
                parser = LoadColorMap(files);
                if (parser != null)
                    parsers.Add(parser);
            }

            // Step 5 (optional). Look for new sprites
            if ((saveFlags & SaveFlags.Sprites) == SaveFlags.Sprites)
            {
                parser = LoadSprites(files);
                if (parser != null)
                    parsers.Add(parser);
            }

            // Step 6 (optional). Look for tile map to load
            if ((saveFlags & SaveFlags.Tilemap) == SaveFlags.Tilemap)
            {
                parser = LoadTilemap(files);
                if (parser != null)
                    parsers.Add(parser);
            }

            // Step 7 (optional). Look for fonts to load
            if ((saveFlags & SaveFlags.Fonts) == SaveFlags.Fonts)
            {
                var fontExtension = ".font.png";

                var paths = files.Keys.Where(s => s.EndsWith(fontExtension)).ToArray();

                foreach (var fileName in paths)
                {
                    var fontName = fileName.Split('.')[0];

                    parser = LoadFont(fontName, files[fileName]);
                    if (parser != null)
                        parsers.Add(parser);
                }
            }

            // Step 8 (optional). Look for meta data and override the game
            if ((saveFlags & SaveFlags.Meta) == SaveFlags.Meta)
            {
                parser = LoadMetaData(files);
                if (parser != null)
                    parsers.Add(parser);
            }
            
            // Step 9 (optional). Look for meta data and override the game
            if ((saveFlags & SaveFlags.Sounds) == SaveFlags.Sounds)
            {
                LoadSounds(files);
//                if (parser != null)
//                    parsers.Add(parser);
            }
            
            // Step 10 (optional). Look for meta data and override the game
            if ((saveFlags & SaveFlags.Music) == SaveFlags.Music)
            {
                LoadMusic(files);
//                if (parser != null)
//                    parsers.Add(parser);
            }
            
            // Step 11 (optional). Look for meta data and override the game
            if ((saveFlags & SaveFlags.SaveData) == SaveFlags.SaveData)
            {
                LoadSaveData(files);
//                if (parser != null)
//                    parsers.Add(parser);
            }

            totalParsers = parsers.Count;
            currentParserID = 0;
            
            watch.Stop();

//            UnityEngine.Debug.Log("Parser Setup Time - " + watch.ElapsedMilliseconds);
        }

        public void LoadAll()
        {
            while (completed == false)
                NextParser();
        }


        public void NextParser()
        {
            if (completed)
                return;

            var parser = parsers[currentParserID];

            var watch = Stopwatch.StartNew();

            if (microSteps)
            {
                parser.NextStep();

                if (parser.completed)
                    currentParserID++;
            }
            else
            {
                while (parser.completed == false)
                    parser.NextStep();

                currentParserID++;
            }

            watch.Stop();

//            UnityEngine.Debug.Log("Parser " + currentParserID + " Done " + watch.ElapsedMilliseconds);
        }

        private AbstractParser LoadMetaData(Dictionary<string, byte[]> files)
        {
            var fileName = "info.json";

            if (files.ContainsKey(fileName))
            {
                var fileContents = Encoding.UTF8.GetString(files[fileName]);
                
                return new MetaDataParser(fileContents, targetEngine);
            }

            return null;
        }

        private AbstractParser LoadFont(string fontName, byte[] data)
        {
            var tex = ReadTexture(data);

            //var fontName = fileSystem.GetFileNameWithoutExtension(file);
            //fontName = fontName.Substring(0, fontName.Length - 5);

            return new FontParser(tex, targetEngine, colorFactory, fontName);
        }

        private AbstractParser LoadTilemap(Dictionary<string, byte[]> files)
        {
            var tilemapFile = "tilemap.png";
            var tilemapJsonFile = "tilemap.json";
            
            // If a tilemap json file exists, try to load that
            if (files.ContainsKey(tilemapJsonFile))
            {
                var fileContents = Encoding.UTF8.GetString(files[tilemapJsonFile]);

                var jsonParser = new TilemapJsonParser(fileContents, targetEngine);
                
                return jsonParser;
            }
            
            // If a tilemap file exists, load that instead
            else if (files.ContainsKey(tilemapFile))
            {
                var tex = ReadTexture(files[tilemapFile]);
                ITexture2D flagTex = null;
                ITexture2D colorTex = null;
                
                var flagFile = "tilemap-flags.png";

                if (files.ContainsKey(flagFile))
                {
                    flagTex = ReadTexture(files[flagFile]);
                }
                
                var colorFile = "tilemap-colors.png";

                if (files.ContainsKey(colorFile))
                {
                    colorTex = ReadTexture(files[colorFile]);
                }
                
                return new TilemapParser(tex, flagTex, colorTex, targetEngine, colorFactory);
            }

            return null;
        }

        private AbstractParser LoadSprites(Dictionary<string, byte[]> files)
        {
            // TODO need to tell if the cache should be ignore, important when in tools
            var srcFile = "sprites.png";
            var cacheFile = "sprites.cache.png";

            string fileName = null;

            if (files.ContainsKey(cacheFile))
            {
                fileName = cacheFile;
            }
            else if(files.ContainsKey(srcFile))
            {
                fileName = srcFile;
            }
            
            if (fileName != null)
            {
                
                var tex = ReadTexture(files[fileName]);

                return new SpriteParser(tex, targetEngine, colorFactory);
            }

            return null;
        }

        private AbstractParser LoadColorMap(Dictionary<string, byte[]> files)
        {
            var fileName = "color-map.png";

            if (files.ContainsKey(fileName))
            {
                var tex = ReadTexture(files[fileName]);

                return new ColorMapParser(tex, targetEngine, colorFactory.magenta);
            }

            return null;
        }

        private AbstractParser LoadColors(Dictionary<string, byte[]> files)
        {
            var fileName = "colors.png";

            if (files.ContainsKey(fileName))
            {
                var tex = ReadTexture(files[fileName]);

                return new ColorParser(tex, targetEngine, colorFactory.magenta);
            }

            return null;
        }

        private ScriptParser LoadScript(string fileName, byte[] data)
        {
            
            var script = Encoding.UTF8.GetString(data);
            var scriptParser = new ScriptParser(fileName, script, targetEngine.gameChip as LuaGameChip);

            return scriptParser;

        }

        private void LoadSystem(Dictionary<string, byte[]> files)
        {
            var fileName = "data.json";

            if (files.ContainsKey(fileName))
            {
                var fileContents = Encoding.UTF8.GetString(files[fileName]);

                var jsonParser = new SystemParser(fileContents, targetEngine);
                while (jsonParser.completed == false)
                    jsonParser.NextStep();
            }
            else
            {
                throw new Exception("Can't find 'data.json' file");
            }
        }

        public ITexture2D ReadTexture(byte[] data)
        {
            // Create a texture to store data in
            var tex = textureFactory.NewTexture2D(1, 1);

            // Load bytes into texture
            tex.LoadImage(data);

            // Return texture
            return tex;
        }
        
        private void LoadSounds(Dictionary<string, byte[]> files)
        {
            var fileName = "sounds.json";

            if (files.ContainsKey(fileName))
            {
                var fileContents = Encoding.UTF8.GetString(files[fileName]);

                var jsonParser = new SystemParser(fileContents, targetEngine);
                while (jsonParser.completed == false)
                    jsonParser.NextStep();
            }

        }
        
        private void LoadMusic(Dictionary<string, byte[]> files)
        {
            var fileName = "music.json";

            if (files.ContainsKey(fileName))
            {
                var fileContents = Encoding.UTF8.GetString(files[fileName]);

                var jsonParser = new SystemParser(fileContents, targetEngine);
                while (jsonParser.completed == false)
                    jsonParser.NextStep();
            }

        }
        
        private void LoadSaveData(Dictionary<string, byte[]> files)
        {
            var fileName = "saves.json";

            if (files.ContainsKey(fileName))
            {
                var fileContents = Encoding.UTF8.GetString(files[fileName]);
                var jsonParser = new SystemParser(fileContents, targetEngine);
                while (jsonParser.completed == false)
                    jsonParser.NextStep();
            }

        }

    }

}