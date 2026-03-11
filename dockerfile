FROM nginx:alpine

COPY . /usr/share/nginx/html

RUN rm /usr/share/nginx/html/index.html

COPY index.html /usr/share/nginx/html/index.html

EXPOSE 80