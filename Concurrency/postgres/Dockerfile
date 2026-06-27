FROM python:3.12-slim
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY . .
# -u = unbuffered, so `docker compose up` shows output line-by-line in real time.
CMD ["python", "-u", "demo.py"]
