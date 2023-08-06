#version 460 core

in vec2 aPosition;

out vec3 normal;

// Camera position + snap offset
uniform vec2 modelPos;
uniform mat4 viewMat;
uniform mat4 projectionMat;
uniform int n;

uniform isampler2D tile00;

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

float getHeight(vec2 pos) {
    //TODO: multiple textures
    vec2 posFloored = floor(pos);
    if (posFloored.x < 0.0 || posFloored.x >= 2048.0) return 0.0;
    if (posFloored.y < 0.0 || posFloored.y >= 2048.0) return 0.0;
    return texture(tile00, posFloored / 2048.0).r / 100.0;
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
    vec2 pos = relativPos + modelPos;
    
    //Heights
    vec3 grid = vec3(scale, 0.0, scale);
    float yHere = getHeight(pos);
    float yLeft = getHeight(pos - grid.xy);
    float yRight = getHeight(pos + grid.xy);
    float yTop = getHeight(pos - grid.yz);
    float yBottom = getHeight(pos + grid.yz);
    float yTopLeft = getHeight(pos - grid.xz);
    float yBottomRight = getHeight(pos + grid.xz);
    
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

    // Implement seam calculation for normal calculation? (Probably not neaded since seams only appear
    // when lowering detail anyway so slight artifacts shadows are not noticable)
    // Sum neighbouring triangles normals (optimized)
    float scale2 = scale * scale;
    float scale4 = scale2 * scale2;
    
    float y0 = yHere - yRight;
    float y1 = yLeft - yHere;
    float y2 = yTopLeft - yTop;
    float y3 = yBottom - yBottomRight;
    
    float y4 = yHere - yBottom;
    float y5 = yTop - yHere;
    float y6 = yTopLeft - yLeft;
    float y7 = yRight - yBottomRight;
    
    float y10 = y0 * y0;
    float y11 = y1 * y1;
    float y12 = y2 * y2;
    float y13 = y3 * y3;
    float y14 = y4 * y4;
    float y15 = y5 * y5;
    float y16 = y6 * y6;
    float y17 = y7 * y7;
    
    float y20 = sqrt(scale4 + scale2 * (y10 + y15));
    float y21 = sqrt(scale4 + scale2 * (y10 + y17));
    float y22 = sqrt(scale4 + scale2 * (y11 + y14));
    float y23 = sqrt(scale4 + scale2 * (y11 + y16));
    float y24 = sqrt(scale4 + scale2 * (y12 + y15));
    float y25 = sqrt(scale4 + scale2 * (y13 + y14));
    
    normal = normalize(vec3(
                           scale  * (y0  / y20 + y0  / y21 + y1  / y22 + y1  / y23 + y2  / y24 + y3  / y25),
                           scale2 * (1.0 / y20 + 1.0 / y21 + 1.0 / y22 + 1.0 / y23 + 1.0 / y24 + 1.0 / y25),
                           scale  * (y4  / y22 + y4  / y25 + y5  / y20 + y5  / y24 + y7  / y21 + y6  / y23)
                       ));

    gl_Position = vec4(vec3(relativPos.x, height, relativPos.y), 1.0) * viewMat * projectionMat;
}