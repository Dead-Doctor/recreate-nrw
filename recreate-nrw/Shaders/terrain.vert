#version 460 core

in vec2 aPosition;

out vec2 pos;

// Camera position + snap offset
uniform vec2 modelPos;
uniform mat4 viewMat;
uniform mat4 projectionMat;
uniform int n;

uniform int textureBaseSize;
uniform int texturesPerLod;

//TODO: Upsample heightmap using bicubic interpolation (decrease size of triangles)
uniform sampler2DArray tileData;
uniform sampler1D tilePos;

float invert(float a)
{
    return 1.0 - a;
}

float notZero(float a)
{
    return min(a, 1.0);
}

float or(float a, float b)
{
    return min(a + b, 1.0);
}

float and(float a, float b)
{
    return a * b;
}

float xor(float a, float b)
{
    return mod(a + b, 2.0);
}

// Expects integers
float getHeight(vec2 pos) {
    int tileCount = textureSize(tilePos, 0);
    int lodCount = tileCount / texturesPerLod;
    ivec3 samplePosition = ivec3(0);
    for (int lod = lodCount - 1; lod >= 0; lod--) {
        int stepSize = 1 << lod;
        int tileSize = textureBaseSize * stepSize;
        vec2 offsetInTile = mod(mod(pos, tileSize) + tileSize, tileSize);
        ivec2 uv = ivec2(round(offsetInTile / stepSize));
        vec2 currentTilePos = floor((pos - offsetInTile) / textureBaseSize);
        for (int i = 0; i < texturesPerLod; i++) {
            int index = lod * texturesPerLod + i;
            vec2 texturePositon = texelFetch(tilePos, index, 0).xy;
            samplePosition = texturePositon == currentTilePos ? ivec3(uv, index) : samplePosition;
        }
    }
    return texelFetch(tileData, samplePosition, 0).r;
}

void main()
{
    float index = gl_InstanceID;
    float corner = mod(index, 4.0);
    float indexWithinCorner = floor(index / 4.0);

    float leftSide = abs(ceil(corner / 2.0) - 1.0);
    float topSide = invert(floor(corner / 2.0));

    float posIndex = mod(indexWithinCorner - 1.0, 3.0);
    float posInside = and(notZero(posIndex), notZero(indexWithinCorner));
    float posLeft = posIndex - posInside;
    float posRight = invert(posLeft);
    float posLeftIsHorizontallyInside = xor(leftSide, topSide);
    float posIsHorizontallyInside = and(posInside, xor(posRight, posLeftIsHorizontallyInside));
    float posIsVerticallyInside = and(posInside, xor(posLeft, posLeftIsHorizontallyInside));
    vec2 posOffsetDirection = vec2(and(posIsHorizontallyInside, leftSide * 2.0 - 1.0), and(posIsVerticallyInside, topSide * 2.0 - 1.0));

    float level = floor((indexWithinCorner - 1.0) / 3.0);
    float scale = pow(2.0, max(level, 0.0));

    vec2 offsetDirection = vec2(1.0 - leftSide * 2, 1.0 - topSide * 2);
    float offsetCount = floor(pow(2.0, level));

    // Shift chunk to correct corner
    vec2 relativPos = aPosition - n * vec2(leftSide, topSide);
    // Scale chunk
    relativPos *= scale;
    // Shift to correct ring
    relativPos += offsetCount * n * offsetDirection;
    // Shift to correct position within ring
    relativPos += scale * n * posOffsetDirection;
    // floor to mitigate floating-point-precision errors
    pos = floor(modelPos + relativPos + 1e-2);
    
    //Heights
    vec3 grid = vec3(scale, 0.0, scale);
    float yHere = getHeight(pos);
    float yLeft = getHeight(pos - grid.xy);
    float yRight = getHeight(pos + grid.xy);
    float yTop = getHeight(pos - grid.yz);
    float yBottom = getHeight(pos + grid.yz);
    
    //Fix seams
    vec2 edge = aPosition / n;

    float leftEdgeOfTile = mod(ceil(edge.x + 1.0), 2.0);
    float rightEdgeOfTile = floor(edge.x);
    float topEdgeOfTile = mod(ceil(edge.y + 1.0), 2.0);
    float bottomEdgeOfTile = floor(edge.y);

    float leftEdgeOfTileLeft = and(leftEdgeOfTile, leftSide);
    float rightEdgeOfTileRight = and(rightEdgeOfTile, invert(leftSide));
    float topEdgeOfTileTop = and(topEdgeOfTile, topSide);
    float bottomEdgeOfTileBottom = and(bottomEdgeOfTile, invert(topSide));

    float horizontalEdgeOfLevel = and(invert(posIsHorizontallyInside), or(leftEdgeOfTileLeft, rightEdgeOfTileRight));
    float verticalEdgeOfLevel = and(invert(posIsVerticallyInside), or(topEdgeOfTileTop, bottomEdgeOfTileBottom));

    float tileCouldHaveSeams = notZero(level + 1.0);
    float oddVertex = or(mod(aPosition.x, 2.0), mod(aPosition.y, 2.0));
    float vertexCouldHaveSeams = and(tileCouldHaveSeams, oddVertex);

    float horizontalSeam = and(horizontalEdgeOfLevel, vertexCouldHaveSeams);
    float verticalInterpolation = (yTop + yBottom) * 0.5;
    
    float verticalSeam = and(verticalEdgeOfLevel, vertexCouldHaveSeams);
    float horizontalInterpolation = (yLeft + yRight) * 0.5;

    float height = mix(mix(yHere, horizontalInterpolation, verticalSeam), verticalInterpolation, horizontalSeam);
    
    gl_Position = vec4(vec3(relativPos.x, height, relativPos.y), 1.0) * viewMat * projectionMat;
}