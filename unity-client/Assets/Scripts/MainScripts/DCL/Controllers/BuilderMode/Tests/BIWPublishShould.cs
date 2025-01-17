﻿using System.Collections;
using System.Collections.Generic;
using DCL.Components;
using DCL.Helpers;
using DCL.Models;
using Newtonsoft.Json;
using NSubstitute.Extensions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class BIWPublishShould : IntegrationTestSuite_Legacy
{
    private BuilderInWorldController controller;
    private BIWPublishController biwPublishController;
    private BuilderInWorldEntityHandler biwEntityHandler;

    private const string entityId = "E1";

    protected override IEnumerator SetUp()
    {
        yield return base.SetUp();
        controller = Resources.FindObjectsOfTypeAll<BuilderInWorldController>()[0];

        controller.InitGameObjects();
        controller.FindSceneToEdit();
        controller.InitControllers();

        biwPublishController = controller.biwPublishController;
        biwPublishController.EnterEditMode(scene);

        biwEntityHandler = controller.builderInWorldEntityHandler;
        biwEntityHandler.EnterEditMode(scene);
    }

    [Test]
    public void TestEntityOutsidePublish()
    {
        //Arrange
        DCLBuilderInWorldEntity entity = biwEntityHandler.CreateEmptyEntity(scene, Vector3.zero, Vector3.zero);

        //Act
        entity.gameObject.transform.position = Vector3.one * 9999;

        //Assert
        Assert.IsFalse(biwPublishController.CanPublish());
    }

    [UnityTest]
    public IEnumerator TestEntityInsidePublish()
    {
        //Arrange
        DCLBuilderInWorldEntity entity = biwEntityHandler.CreateEmptyEntity(scene, Vector3.zero, Vector3.zero);
        TestHelpers.CreateAndSetShape(scene, entity.rootEntity.entityId, DCL.Models.CLASS_ID.GLTF_SHAPE, JsonConvert.SerializeObject(
            new
            {
                src = Utils.GetTestsAssetsPath() + "/GLB/Trunk/Trunk.glb"
            }));

        LoadWrapper gltfShape = GLTFShape.GetLoaderForEntity(scene.entities[entity.rootEntity.entityId]);
        yield return new WaitUntil(() => gltfShape.alreadyLoaded);

        //Act
        entity.rootEntity.gameObject.transform.position = new Vector3(5, 0, 5);

        //Assert
        Assert.IsTrue(biwPublishController.CanPublish());
    }

    [Test]
    public void TestMetricsPublish()
    {
        //Act
        for (int i = 0; i < scene.metricsController.GetLimits().entities + 1; i++)
        {
            TestHelpers.CreateSceneEntity(scene, entityId + i);
        }

        //Assert
        Assert.IsFalse(biwPublishController.CanPublish());
    }

    protected override IEnumerator TearDown()
    {
        controller.CleanItems();
        yield return base.TearDown();
    }
}