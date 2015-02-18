﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.
using System;
using System.Collections.Generic;

using SiliconStudio.Paradox.Extensions;
using SiliconStudio.Core.Collections;
using SiliconStudio.Paradox.Graphics;

namespace SiliconStudio.Paradox.Effects
{
    /// <summary>
    /// An effect mesh.
    /// </summary>
    public class RenderMesh : DynamicEffectInstance
    {
        private VertexArrayObject vertexArrayObject;
        private VertexArrayObject vertexArrayObjectAEN;

        /// <summary>
        /// The model instance associated to this effect mesh.
        /// </summary>
        /// <value>The model instance.</value>
        public readonly RenderModel RenderModel;

        /// <summary>
        /// The mesh associated with this instance.
        /// </summary>
        public readonly Mesh Mesh;

        public Material Material;

        private readonly ParameterCollection parameters;

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderMesh" /> class.
        /// </summary>
        /// <param name="renderModel">The render model.</param>
        /// <param name="mesh">The mesh data.</param>
        /// <exception cref="System.ArgumentNullException">mesh</exception>
        public RenderMesh(RenderModel renderModel, Mesh mesh)
        {
            if (renderModel == null) throw new ArgumentNullException("renderModel");
            if (mesh == null) throw new ArgumentNullException("mesh");
            RenderModel = renderModel;
            Mesh = mesh;
            Enabled = true;

            UpdateMaterial();

            // A RenderMesh is inheriting values from Mesh.Parameters
            // We are considering that Mesh.Parameters is not updated frequently (should be almost immutable)
            parameters = new ParameterCollection();
            if (mesh.Parameters != null)
            {
                parameters.AddSources(mesh.Parameters);
            }
        }

        /// <summary>
        /// Enable or disable this particular effect mesh.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets the parameters.
        /// </summary>
        /// <value>The parameters.</value>
        public ParameterCollection Parameters
        {
            get
            {
                return parameters;
            }
        }

        /// <summary>
        /// Draw this effect mesh.
        /// </summary>
        public void Draw(RenderContext context)
        {
            // Retrieve effect parameters
            var currentPass = context.CurrentPass;
            var mesh = Mesh;
            var currentRenderData = mesh.Draw;
            var material = Material;
            var tessellationMethod = material.TessellationMethod;

            // adapt the primitive type and index buffer to the tessellation used
            var vao = vertexArrayObject;
            var drawCount = currentRenderData.DrawCount;
            if (tessellationMethod.PerformsAdjacentEdgeAverage())
            {
                vao = GetOrCreateVertexArrayObjectAEN(context);
                drawCount = 12 / 3 * drawCount;
            }
            currentRenderData.PrimitiveType = tessellationMethod.GetPrimitiveType();


            //using (Profiler.Begin(ProfilingKeys.PrepareMesh))
            {
                // Order of application of parameters:
                // - RenderPass.Parameters
                // - ModelComponent.Parameters
                // - RenderMesh.Parameters (originally copied from mesh parameters)
                // The order is based on the granularity level of each element and how shared it can be. Material is heavily shared, a model contains many meshes. An renderMesh is unique.
                // TODO: really copy mesh parameters into renderMesh instead of just referencing the meshDraw parameters.

                var modelComponent = RenderModel.ModelInstance;
                var hasModelComponentParams = modelComponent != null && modelComponent.Parameters != null;
                
                var materialParameters = material != null && material.Parameters != null ? material.Parameters : null;

                if (materialParameters != null)
                {
                    if (hasModelComponentParams)
                        Effect.Apply(currentPass.Parameters, materialParameters, modelComponent.Parameters, parameters, true);
                    else
                        Effect.Apply(currentPass.Parameters, materialParameters, parameters, true);
                }
                else if (hasModelComponentParams)
                    Effect.Apply(currentPass.Parameters, modelComponent.Parameters, parameters, true);
                else
                    Effect.Apply(currentPass.Parameters, parameters, true);
            }

            //using (Profiler.Begin(ProfilingKeys.RenderMesh))
            {
                if (currentRenderData != null)
                {
                    var graphicsDevice = context.GraphicsDevice;

                    graphicsDevice.SetVertexArrayObject(vao);

                    if (currentRenderData.IndexBuffer == null)
                    {
                        graphicsDevice.Draw(currentRenderData.PrimitiveType, drawCount, currentRenderData.StartLocation);
                    }
                    else
                    {
                        graphicsDevice.DrawIndexed(currentRenderData.PrimitiveType, drawCount, currentRenderData.StartLocation);
                    }
                }
            }
        }

        private VertexArrayObject GetOrCreateVertexArrayObjectAEN(RenderContext context)
        {
            if (vertexArrayObjectAEN == null)
            {
                var indexBufferAEN = IndexExtensions.GenerateIndexBufferAEN(Mesh.Draw.IndexBuffer, Mesh.Draw.VertexBuffers[0]);
                vertexArrayObjectAEN = VertexArrayObject.New(context.GraphicsDevice, Effect.InputSignature, indexBufferAEN, Mesh.Draw.VertexBuffers);
            }

            return vertexArrayObjectAEN;
        }

        public void UpdateMaterial()
        {
            Material = RenderModel.GetMaterial(Mesh.MaterialIndex);
        }

        internal void Initialize(GraphicsDevice device)
        {
            vertexArrayObject = VertexArrayObject.New(device, Effect.InputSignature, Mesh.Draw.IndexBuffer, Mesh.Draw.VertexBuffers);
        }

        public override void FillParameterCollections(FastList<ParameterCollection> parameterCollections)
        {
            var material = Material;
            if (material != null && material.Parameters != null)
            {
                parameterCollections.Add(material.Parameters);
            }

            var modelInstance = RenderModel.ModelInstance;
            if (modelInstance != null && modelInstance.Parameters != null)
            {
                parameterCollections.Add(modelInstance.Parameters);
            }

            parameterCollections.Add(parameters);
        }
    }
}
