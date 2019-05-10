using System.Collections;
using System.Collections.Generic;
using DCL.Components;
using DCL.Controllers;
using DCL.Helpers;
using DCL.Models;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Newtonsoft.Json;

namespace Tests
{
    public class TransformTests : TestsBase
    {
        [UnityTest]
        public IEnumerator TransformUpdate()
        {
            yield return InitScene();

            string entityId = "1";
            TestHelpers.CreateSceneEntity(scene, entityId);

            var entityObject = scene.entities[entityId];

            Assert.IsTrue(entityObject != null);

            {
                Vector3 originalTransformPosition = entityObject.gameObject.transform.position;
                Quaternion originalTransformRotation = entityObject.gameObject.transform.rotation;
                Vector3 originalTransformScale = entityObject.gameObject.transform.localScale;

                Vector3 position = new Vector3(5, 1, 5);
                Quaternion rotationQuaternion = Quaternion.Euler(10, 50, -90);
                Vector3 scale = new Vector3(0.7f, 0.7f, 0.7f);

                string rawJSON = JsonConvert.SerializeObject(new
                {
                    entityId = entityId,
                    name = "transform",
                    classId = CLASS_ID_COMPONENT.TRANSFORM,
                    json = JsonConvert.SerializeObject(new
                    {
                        position = position,
                        rotation = new
                        {
                            x = rotationQuaternion.x,
                            y = rotationQuaternion.y,
                            z = rotationQuaternion.z,
                            w = rotationQuaternion.w
                        },
                        scale = scale
                    })
                });

                Assert.IsTrue(!string.IsNullOrEmpty(rawJSON));

                scene.EntityComponentCreate(rawJSON);

                Assert.AreNotEqual(originalTransformPosition, entityObject.gameObject.transform.position);
                Assert.AreEqual(position, entityObject.gameObject.transform.position);

                Assert.AreNotEqual(originalTransformRotation, entityObject.gameObject.transform.rotation);
                Assert.AreEqual(rotationQuaternion.ToString(), entityObject.gameObject.transform.rotation.ToString());

                Assert.AreNotEqual(originalTransformScale, entityObject.gameObject.transform.localScale);
                Assert.AreEqual(scale, entityObject.gameObject.transform.localScale);
            }

            {
                Vector3 originalTransformPosition = entityObject.gameObject.transform.position;
                Quaternion originalTransformRotation = entityObject.gameObject.transform.rotation;
                Vector3 originalTransformScale = entityObject.gameObject.transform.localScale;

                Vector3 position = new Vector3(51, 13, 52);
                Quaternion rotationQuaternion = Quaternion.Euler(101, 51, -91);
                Vector3 scale = new Vector3(1.7f, 3.7f, -0.7f);

                string rawJSON = JsonConvert.SerializeObject(new EntityComponentCreateMessage
                {
                    entityId = entityId,
                    name = "transform",
                    classId = (int)CLASS_ID_COMPONENT.TRANSFORM,
                    json = JsonConvert.SerializeObject(new
                    {
                        position = position,
                        rotation = new
                        {
                            x = rotationQuaternion.x,
                            y = rotationQuaternion.y,
                            z = rotationQuaternion.z,
                            w = rotationQuaternion.w
                        },
                        scale = scale
                    })
                });

                Assert.IsTrue(!string.IsNullOrEmpty(rawJSON));

                scene.EntityComponentCreate(rawJSON);

                Assert.AreNotEqual(originalTransformPosition, entityObject.gameObject.transform.position);
                Assert.AreEqual(position, entityObject.gameObject.transform.position);

                Assert.AreNotEqual(originalTransformRotation, entityObject.gameObject.transform.rotation);
                Assert.AreEqual(rotationQuaternion.ToString(), entityObject.gameObject.transform.rotation.ToString());

                Assert.AreNotEqual(originalTransformScale, entityObject.gameObject.transform.localScale);
                Assert.AreEqual(scale, entityObject.gameObject.transform.localScale);
            }

            {
                Vector3 originalTransformPosition = entityObject.gameObject.transform.position;
                Quaternion originalTransformRotation = entityObject.gameObject.transform.rotation;
                Vector3 originalTransformScale = entityObject.gameObject.transform.localScale;

                Vector3 position = new Vector3(0, 0, 0);
                Quaternion rotationQuaternion = Quaternion.Euler(0, 0, 0);
                Vector3 scale = new Vector3(1, 1, 1);

                string rawJSON = JsonUtility.ToJson(new EntityComponentRemoveMessage
                {
                    entityId = entityId,
                    name = "transform"
                });

                Assert.IsTrue(!string.IsNullOrEmpty(rawJSON));

                scene.EntityComponentRemove(rawJSON);

                yield return null;

                Assert.AreNotEqual(originalTransformPosition, entityObject.gameObject.transform.position);
                Assert.AreEqual(position, entityObject.gameObject.transform.position);

                Assert.AreNotEqual(originalTransformRotation, entityObject.gameObject.transform.rotation);
                Assert.AreEqual(rotationQuaternion.ToString(), entityObject.gameObject.transform.rotation.ToString());

                Assert.AreNotEqual(originalTransformScale, entityObject.gameObject.transform.localScale);
                Assert.AreEqual(scale, entityObject.gameObject.transform.localScale);
            }
        }

        [UnityTest]
        public IEnumerator TransformComponentMissingValuesGetDefaultedOnUpdate()
        {
            yield return InitScene();

            string entityId = "1";
            TestHelpers.CreateSceneEntity(scene, entityId);

            // 1. Create component with non-default configs
            DCLTransform.Model componentModel = new DCLTransform.Model
            {
                position = new Vector3(3f, 7f, 1f),
                rotation = new Quaternion(4f, 9f, 1f, 7f),
                scale = new Vector3(5f, 0.7f, 2f)
            };

            DCLTransform transformComponent = TestHelpers.EntityComponentCreate<DCLTransform, DCLTransform.Model>(scene, scene.entities[entityId], componentModel);

            // 2. Check configured values
            Assert.AreEqual(new Vector3(3f, 7f, 1f), transformComponent.model.position);
            Assert.AreEqual(new Quaternion(4f, 9f, 1f, 7f), transformComponent.model.rotation);
            Assert.AreEqual(new Vector3(5f, 0.7f, 2f), transformComponent.model.scale);

            // 3. Update component with missing values
            componentModel = new DCLTransform.Model
            {
                position = new Vector3(30f, 70f, 10f)
            };

            scene.EntityComponentUpdate(scene.entities[entityId], CLASS_ID_COMPONENT.TRANSFORM, JsonUtility.ToJson(componentModel));

            // 4. Check changed values
            Assert.AreEqual(new Vector3(30f, 70f, 10f), transformComponent.model.position);

            // 5. Check defaulted values
            Assert.AreEqual(Quaternion.identity, transformComponent.model.rotation);
            Assert.AreEqual(Vector3.one, transformComponent.model.scale);
        }

        [UnityTest]
        public IEnumerator TransformationsAreKeptRelativeAfterParenting()
        {
            yield return InitScene();

            string entityId = "1";
            TestHelpers.CreateSceneEntity(scene, entityId);

            Vector3 targetPosition = new Vector3(3f, 7f, 1f);
            Quaternion targetRotation = new Quaternion(4f, 9f, 1f, 7f);
            Vector3 targetScale = new Vector3(5f, 0.7f, 2f);

            // 1. Create component with non-default configs
            DCLTransform.Model componentModel = new DCLTransform.Model
            {
                position = targetPosition,
                rotation = targetRotation,
                scale = targetScale
            };
            DCLTransform transformComponent = TestHelpers.EntityComponentCreate<DCLTransform, DCLTransform.Model>(scene, scene.entities[entityId], componentModel);

            // 2. Check configured values
            Assert.IsTrue(targetPosition == transformComponent.entity.gameObject.transform.localPosition);
            Assert.IsTrue(targetRotation == transformComponent.entity.gameObject.transform.localRotation);
            Assert.IsTrue(targetScale == transformComponent.entity.gameObject.transform.localScale);

            // 3. Create new parent entity
            string parentEntityId = "2";
            TestHelpers.CreateSceneEntity(scene, parentEntityId);
            componentModel = new DCLTransform.Model
            {
                position = new Vector3(15f, 56f, 0f),
                rotation = new Quaternion(1f, 3f, 5f, 15f),
                scale = new Vector3(2f, 3f, 5f)
            };

            // 4. set new parent
            TestHelpers.SetEntityParent(scene, entityId, parentEntityId);

            // 5. check transform values remains the same
            Assert.IsTrue(targetPosition == transformComponent.entity.gameObject.transform.localPosition);
            Assert.IsTrue(targetRotation == transformComponent.entity.gameObject.transform.localRotation);
            Assert.IsTrue(targetScale == transformComponent.entity.gameObject.transform.localScale);
        }
    }
}
