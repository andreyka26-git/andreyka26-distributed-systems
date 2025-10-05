# Redis Hashslot Sharding Kubernetes Deployment

This directory contains Kubernetes manifests to deploy the Redis Hashslot Sharding application.

## Prerequisites

1. **Kubernetes cluster** (local or cloud)
   - For local development: Docker Desktop with Kubernetes enabled, minikube, or kind
   - For cloud: AKS, EKS, GKE, etc.

2. **kubectl** configured to communicate with your cluster

3. **Docker image** built and available in your cluster
   - For local clusters: build the image locally
   - For cloud clusters: push to a container registry (Docker Hub, ACR, ECR, etc.)

## Building and Deploying

### Step 1: Build the Docker Image

```bash
# Build the image locally
docker build -t redis-hashslot-sharding:latest ./RedisHashslotSharding

# For cloud deployment, tag and push to your registry
docker tag redis-hashslot-sharding:latest your-registry/redis-hashslot-sharding:latest
docker push your-registry/redis-hashslot-sharding:latest
```

### Step 2: Deploy to Kubernetes

```bash
# Apply all manifests
kubectl apply -f k8s/

# Or apply them individually in order
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml
```

### Step 3: Verify Deployment

```bash
# Check if all resources are created
kubectl get all -n redis-hashslot-sharding

# Check pod logs
kubectl logs -n redis-hashslot-sharding deployment/redis-hashslot-sharding-deployment

# Get service details
kubectl get svc -n redis-hashslot-sharding
```

### Step 4: Access the Application

```bash
# For LoadBalancer service (cloud providers)
kubectl get svc redis-hashslot-sharding-service -n redis-hashslot-sharding

# For local development with port-forward
kubectl port-forward -n redis-hashslot-sharding svc/redis-hashslot-sharding-service 8080:80
```

## Configuration

### Environment Variables
Environment variables are managed through the ConfigMap in `configmap.yaml`. Update this file to modify:
- `ASPNETCORE_ENVIRONMENT`
- `ASPNETCORE_URLS`
- Add any additional configuration

### Scaling
To scale the application:
```bash
kubectl scale deployment redis-hashslot-sharding-deployment --replicas=5 -n redis-hashslot-sharding
```

### Resource Limits
Adjust CPU and memory limits in `deployment.yaml` under the `resources` section.

## Health Checks

The deployment includes:
- **Liveness Probe**: Checks if the application is running
- **Readiness Probe**: Checks if the application is ready to receive traffic

Both probes expect a `/health` endpoint. If your application doesn't have this endpoint, either:
1. Add a health check endpoint to your .NET application
2. Remove or modify the probe configuration in `deployment.yaml`

## Service Types

The current service is configured as `LoadBalancer`. You can change this based on your needs:

- **ClusterIP**: Internal access only
- **NodePort**: Access via node IP and port
- **LoadBalancer**: External load balancer (requires cloud provider support)
- **Ingress**: HTTP/HTTPS routing (requires ingress controller)

## Cleanup

To remove all resources:
```bash
kubectl delete -f k8s/
# or
kubectl delete namespace redis-hashslot-sharding
```