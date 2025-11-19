using System;
using System.Diagnostics;
using UnityEngine;
using Random = UnityEngine.Random;

public class InkToParticles : MonoBehaviour
{
    public GameObject particlePrefab;     
    public int particlesPerUnit = 10;     
    public float fadeDuration = 1f;      
    public float scaleDuration = 0.8f;    
    public float rotationSpeed = 180f;   
    public Color particleColor = new Color(1f, 0.5f, 0f);

    public void ConvertLineToParticles(LineRenderer line)
    {
        if (line == null) return;

        int pointCount = line.positionCount;
        if (pointCount < 2) return;

        // 检查线条是否有效
        for (int i = 0; i < pointCount; i++)
        {
            Vector3 pos = line.GetPosition(i);
            if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z))
            {
                return;
            }
        }

        // 启动协程，逐渐转换
        StartCoroutine(ConvertLineGradually(line));
    }
    System.Collections.IEnumerator ConvertLineGradually(LineRenderer line)
    {
        int pointCount = line.positionCount;

        // 先保存所有点的位置
        Vector3[] allPositions = new Vector3[pointCount];
        for (int i = 0; i < pointCount; i++)
        {
            allPositions[i] = line.GetPosition(i);
        }

        Color originalColor = line.startColor;

        if (line != null)
        {
            Material lineMat = line.material;
            if (line != null)
            {
                line.material = new Material(line.material);
                line.material.renderQueue = 2000;
            }
        }

        // 计算线条总长度
        float totalLength = 0;
        for (int i = 1; i < pointCount; i++)
        {
            totalLength += Vector3.Distance(allPositions[i - 1], allPositions[i]);
        }

        int particleCount = Mathf.CeilToInt(totalLength * particlesPerUnit);
        System.Collections.Generic.List<GameObject> particles = new System.Collections.Generic.List<GameObject>();

        // 隐藏线条
        if (line != null)
        {
            line.positionCount = 0;
        }

        // 总动画时长
        float totalDuration = 0.4f;
        float lineDelay = 0.3f;
        float timer = 0f;

        int lastParticleIndex = 0;
        int lastActivatedParticle = 0;

        while (timer < totalDuration + lineDelay)
        {
            timer += Time.deltaTime;

            // 1. 粒子生成
            float particleProgress = Mathf.Clamp01(timer / totalDuration);
            int currentParticleIndex = Mathf.FloorToInt(particleCount * particleProgress);

            for (int i = lastParticleIndex; i < currentParticleIndex; i++)
            {
                float t = (float)i / particleCount;
                Vector3 position = GetPositionOnLineFromArray(allPositions, t);

                if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z))
                {
                    continue;
                }

                GameObject particle = Instantiate(particlePrefab, position, Quaternion.identity);

                Vector3 randomOffset = new Vector3(
                    Random.Range(-0.3f, 0.3f),
                    Random.Range(-0.3f, 0.3f),
                    Random.Range(-0.1f, 0.1f)
                );

                Vector3 toCameraDir = (Camera.main.transform.position - position).normalized;
                particle.transform.position = position + toCameraDir * 0.9f + randomOffset;

                float randomScale = Random.Range(0.3f, 0.8f);
                particle.transform.localScale *= randomScale;

                particle.transform.LookAt(Camera.main.transform);
                particle.transform.Rotate(0, 180, 0);
                particle.transform.Rotate(0, 0, Random.Range(0f, 360f));

                MeshRenderer renderer = particle.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.material != null)
                {
                    renderer.material = new Material(renderer.material);
                    renderer.material.color = particleColor;

                    renderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    renderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    renderer.material.SetInt("_ZWrite", 0);
                    renderer.material.renderQueue = 3100;
                }

                particles.Add(particle);
            }

            lastParticleIndex = currentParticleIndex;

            // 2. 线条重现
            if (timer > lineDelay && line != null)
            {
                float lineProgress = Mathf.Clamp01((timer - lineDelay) / totalDuration);
                int visiblePoints = Mathf.Min(Mathf.Max(Mathf.CeilToInt(pointCount * lineProgress), 2), pointCount);

                line.positionCount = visiblePoints;
                for (int i = 0; i < visiblePoints; i++)
                {
                    line.SetPosition(i, allPositions[i]);
                }

                // 3. 线条追上的粒子开始消散（按顺序）
                int lineParticleIndex = Mathf.FloorToInt(particleCount * lineProgress);

                for (int i = lastActivatedParticle; i < lineParticleIndex && i < particles.Count; i++)
                {
                    if (particles[i] != null)
                    {
                        ParticleFadeOut fadeOut = particles[i].AddComponent<ParticleFadeOut>();
                        fadeOut.fadeDuration = fadeDuration;
                        fadeOut.scaleDuration = scaleDuration;
                        fadeOut.rotationSpeed = Random.Range(100f, 300f);
                        fadeOut.startDelay = 0f;  // 线条追上立即消散
                    }
                }

                lastActivatedParticle = lineParticleIndex;
            }

            yield return null;
        }

        // 确保所有粒子都开始消散（按顺序）
        for (int i = lastActivatedParticle; i < particles.Count; i++)
        {
            if (particles[i] != null)
            {
                ParticleFadeOut fadeOut = particles[i].AddComponent<ParticleFadeOut>();
                fadeOut.fadeDuration = fadeDuration;
                fadeOut.scaleDuration = scaleDuration;
                fadeOut.rotationSpeed = Random.Range(100f, 300f);

                // 剩余粒子按顺序延迟消散
                float delay = (float)(i - lastActivatedParticle) * 0.02f;
                fadeOut.startDelay = delay;
            }
        }

        yield return new WaitForSeconds(0.2f);

        // 线条淡出（从头到尾逐渐消失）
        float fadeOutDuration = 1f;
        timer = 0f;

        while (timer < fadeOutDuration && line != null)
        {
            timer += Time.deltaTime;
            float fadeProgress = timer / fadeOutDuration;

            // 计算还保留多少点（从头开始消失）
            int remainingPoints = Mathf.Max(2, Mathf.CeilToInt(pointCount * (1f - fadeProgress)));

            // 只显示后面的点
            int startPoint = pointCount - remainingPoints;

            line.positionCount = remainingPoints;
            for (int i = 0; i < remainingPoints; i++)
            {
                line.SetPosition(i, allPositions[startPoint + i]);
            }

            yield return null;
        }

        // 销毁线条
        if (line != null)
        {
            line.positionCount = 0;
            Destroy(line.gameObject);
        }
    }


    Vector3 GetPositionOnLineFromArray(Vector3[] positions, float t)
    {
        int pointCount = positions.Length;
        float totalLength = 0;

        for (int i = 1; i < pointCount; i++)
        {
            totalLength += Vector3.Distance(positions[i - 1], positions[i]);
        }

        float targetLength = totalLength * t;
        float currentLength = 0;

        for (int i = 1; i < pointCount; i++)
        {
            Vector3 p1 = positions[i - 1];
            Vector3 p2 = positions[i];
            float segmentLength = Vector3.Distance(p1, p2);

            if (currentLength + segmentLength >= targetLength)
            {
                float segmentT = (targetLength - currentLength) / segmentLength;
                return Vector3.Lerp(p1, p2, segmentT);
            }

            currentLength += segmentLength;
        }

        return positions[pointCount - 1];
    }


    Vector3 GetPositionOnLine(LineRenderer line, float t)
    {
        int pointCount = line.positionCount;
        float totalLength = 0;

        for (int i = 1; i < pointCount; i++)
        {
            totalLength += Vector3.Distance(line.GetPosition(i - 1), line.GetPosition(i));
        }


        float targetLength = totalLength * t;
        float currentLength = 0;

        for (int i = 1; i < pointCount; i++)
        {
            Vector3 p1 = line.GetPosition(i - 1);
            Vector3 p2 = line.GetPosition(i);
            float segmentLength = Vector3.Distance(p1, p2);

            if (currentLength + segmentLength >= targetLength)
            {
                float segmentT = (targetLength - currentLength) / segmentLength;
                return Vector3.Lerp(p1, p2, segmentT);
            }

            currentLength += segmentLength;
        }

        return line.GetPosition(pointCount - 1);
    }
}