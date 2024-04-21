// Using a packed int as a stack, fitting 4 values into a 4-byte int


// --- READ/WRITE AT INDEX

inline uint ReadStackInternal(uint stack, int id)
{
    int internalId = id % 4;
    
    uint extracted = (stack >> (internalId * 8)) & 0xFFu;
    return extracted;
}

inline void WriteStackInternal(inout uint stack, int id, uint value)
{
    int internalId = id % 4;
    
    // Clear the bits at the specified index
    stack &= ~(0xFFu << (internalId * 8));
    
    // Pack the new value
    stack |= (value & 0xFFu) << (internalId * 8);
}


// --- STACK FUNCTIONALITY

void AddStackInternal(inout RayPayload payload, int matId)
{
    int cur = payload.mediumStackCounter;
    
    // Max capacity of 4 materials
	// If the limit is reached, ignore
    if (cur == 3) 
        return;
	
    cur++;
    WriteStackInternal(payload.mediumStack, cur, matId);
    payload.mediumStackCounter = cur;
}

void PopStackInternal(inout RayPayload payload)
{
    int cur = payload.mediumStackCounter;
	
	// No material on the stack
	// Might happen if the mesh is not watertight or has inverted windings
    if (cur == -1)
        return;
	
    WriteStackInternal(payload.mediumStack, cur, 0);
    cur--;

    payload.mediumStackCounter = cur;
}


// --- EXTERNAL FUNCTIONS

MediumData GetCurrentMaterial(inout RayPayload payload)
{
    int cur = payload.mediumStackCounter;
	
	// current id == -1 is air
    if (cur == -1)
        return gMediums[0];
    else
    {
        uint matId = ReadStackInternal(payload.mediumStack, cur);
        return gMediums[matId];
    }
}

uint GetCurrentMaterialId(inout RayPayload payload)
{
    int cur = payload.mediumStackCounter;
	
	// current id == -1 is air
    if (cur == -1)
        return 0;
    else
        return ReadStackInternal(payload.mediumStack, cur);
}

void AddMaterialToStack(inout RayPayload payload, int matId)
{
    int cur = GetCurrentMaterialId(payload);
    AddStackInternal(payload, matId);
}

void PopMaterialFromStack(inout RayPayload payload)
{
    int cur = GetCurrentMaterialId(payload);
    PopStackInternal(payload);
}
